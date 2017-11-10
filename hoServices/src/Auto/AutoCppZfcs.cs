﻿using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DataModels.VcSymbols;
using hoLinqToSql.LinqUtils;
using hoReverse.Services.AutoCpp.Analyze;
using LinqToDB.DataProvider;
using File = System.IO.File;

// ReSharper disable once CheckNamespace
namespace hoReverse.Services.AutoCpp
{
    public partial class AutoCpp
    {
        /// <summary>
        /// Show all external functions for this Component/Class
        /// - Provided
        /// - Required
        /// </summary>
        /// <param name="el"></param>
        /// <returns></returns>
        public bool ShowExternalFunctions(EA.Element el)
        {
            // get connection string of repository
            IDataProvider provider; // the provider to connect to database like Access, ..
            string connectionString = LinqUtil.GetConnectionString(ConnectionString, out provider);
            using (var db = new BROWSEVCDB(provider, connectionString))
            {
                // Estimate file name of component
                string fileNameOfClass = (from f in db.Files
                    where f.LeafName.ToLower() == $"{el.Name.ToLower()}.c" || f.LeafName.ToLower() == $"{el.Name.ToLower()}.h" ||
                          f.LeafName.ToLower() == $"{el.Name.ToLower()}.cpp" || f.LeafName.ToLower() == $"{el.Name.ToLower()}.hpp"
                    select f.Name).FirstOrDefault();
                if (fileNameOfClass == null)
                {
                    MessageBox.Show($"Checked file extensions (*.c,*.h,*.hpp,*.cpp)",
                        $"Cant't find source for '{el.Name}', Break!!");
                    return false;

                }

                string folderNameOfClass = Path.GetDirectoryName(fileNameOfClass);
                if (Path.GetFileName(folderNameOfClass)?.ToLower() != el.Name.ToLower() ) folderNameOfClass = Directory.GetParent(folderNameOfClass).FullName;
                if (Path.GetFileName(folderNameOfClass).ToLower() != el.Name.ToLower() ) folderNameOfClass = Directory.GetParent(folderNameOfClass).FullName;
                if (Path.GetFileName(folderNameOfClass).ToLower() != el.Name.ToLower()) folderNameOfClass = Directory.GetParent(folderNameOfClass).FullName;

                if (Path.GetFileName(folderNameOfClass).ToLower() != el.Name.ToLower())
                {
                    MessageBox.Show($"Checked file extensions (*.c,*.h,*.hpp,*.cpp)\r\nLast checked:{folderNameOfClass}",
                        $"Cant't find source for '{el.Name}', Break!!");
                    return false;
                }

                // estimate file names of component
                // Component and Module implementation file names beneath folder
                IQueryable<string> fileNamesOfClassTree = from f in db.Files
                    where f.Name.StartsWith(folderNameOfClass) && f.LeafName.EndsWith(".c")
                    select f.LeafName;

                // Get all function implementation
                var allFunctionsImpl = (from f in db.CodeItems
                    join file in db.Files on f.FileId equals file.Id
                    where f.Kind == 22 && file.LeafName.ToLower().EndsWith(".c")
                    select new ImplFunctionItem("", f.Name, file.Name)).ToList();
                //select new {Implementation = f.Name, FilePath = file.Name, FileName = file.LeafName}).ToList();


                //var function1 = db.CodeItems.ToList();
                //var functions11 = (from f in function1
                //    join m in _macros on f.Name equals m.Key
                //    where f.Name.ToLower().StartsWith(el.Name.ToLower())
                //    select f.Name).ToList().ToDataTable();
                var allCompImplementations = (
                        // Implemented Interfaces (Macro with Interface and implementation with different name)
                        from m in _macros
                        join f in allFunctionsImpl on m.Key equals f.Implementation
                        where m.Value.ToLower().StartsWith(el.Name.ToLower())
                        select new ImplFunctionItem(m.Value, m.Key, f.FilePath))
                    .Union
                    (from f in allFunctionsImpl

                        where f.Implementation.StartsWith(el.Name)
                        select new ImplFunctionItem(f.Implementation, f.Implementation, f.FilePath))
                    .Union
                    // macros without implementation
                    (from m in _macros
                        where m.Value.ToLower().StartsWith(el.Name.ToLower()) &&
                              allFunctionsImpl.All(f => m.Key != f.Implementation)
                        select new ImplFunctionItem(m.Value, m.Key, ""));
                   
               

                var compImplementations = (from f in allCompImplementations
                    where f.FilePath.StartsWith(folderNameOfClass) || f.FilePath == ""
                    select new
                    {
                        Imp= new ImplFunctionItem(f.Interface, f.Implementation, f.FilePath),
                        RX = new Regex($@"\b{f.Implementation}\s*\(")
                    }).ToArray();
                


                // over all files except Class/Component Tree (files not part of component/class/subfolder)
                var fileNamesCalledImplementation = (from f in db.Files
                    where !fileNamesOfClassTree.Any(x => x == f.LeafName) &&
                          f.LeafName.ToLower().EndsWith(".c")
                    select f.Name).Distinct();

                
                foreach (var fileName in fileNamesCalledImplementation)
                {
                    string code = File.ReadAllText(fileName);
                    code = hoService.DeleteComment(code);
                    int count = 0;
                    foreach (var f1 in compImplementations)
                    {
                        // Call function in code found, the function is a required interface
                        //Match match = f1.RX.Match(code);
                        //if (match.Success)
                        //{
                        count += 1;
                        if (f1.RX.IsMatch(code)) { 
                            //string found = match.Groups[0].Value; 
                            f1.Imp.IsCalled = true;
                            f1.Imp.FilePathCallee = fileName;
                            // string test = found;

                        }
                    }
                }
                // Sort: Function, FileName
                var outputList = (from f in compImplementations
                    orderby f.Imp.Interface, f.Imp.Implementation
                    select new {Interface = f.Imp.Interface,
                        Implementation = f.Imp.Implementation,
                        FileName = f.Imp.FileName,
                        FileNameCalleee = f.Imp.FileNameCallee,
                        FilePathImplementation = f.Imp.FilePath,
                        FilePathCalle = f.Imp.FilePathCallee,
                        isCalled = f.Imp.IsCalled
                    }).Distinct();

                DataTable dt = outputList.ToDataTable();

                // Output Function, FileNme/GUID
                string delimiter = Environment.NewLine;
                string lExternalFunction = $"GUID={el.ElementGUID}{delimiter}FQ={el.FQName}{delimiter}";
                foreach (var row in outputList)
                {
                    string fileNameCalleee = row.FileNameCalleee;
                    EA.Element elComponent = GetElementFromName(Path.GetFileNameWithoutExtension(fileNameCalleee));
                    string guid = elComponent != null ? elComponent.ElementGUID : "";

                    lExternalFunction = $"{lExternalFunction}{delimiter}{row.Interface}/{row.Implementation.PadRight(80)}\t{fileNameCalleee}/{guid}";
                    
                }
  

                Clipboard.SetText(lExternalFunction);
                // new component
                if (_frm == null || _frm.IsDisposed)
                {
                    _frm = new FrmComponentFunctions(el, folderNameOfClass, dt);
                    _frm.Show();
                }
                else
                {
                    _frm.ChangeComponent(el, folderNameOfClass, dt);
                    _frm.Show();
                }
                //frm.ShowDialog();
               
                return true;

            }
        }

        /// <summary>
        /// Inventory paths
        /// </summary>
        /// <param name="backgroundWorker">Background worker to update progress or null</param>
        /// <param name="pathRoot">The path of the root folder to inventory</param>
        /// <returns></returns>
        public bool InventoryMacros(System.ComponentModel.BackgroundWorker backgroundWorker, string pathRoot = "")
        {


            // get connection string of repository
            IDataProvider provider; // the provider to connect to database like Access, ..
            string connectionString = LinqUtil.GetConnectionString(ConnectionString, out provider);
            using (var db = new DataModels.VcSymbols.BROWSEVCDB(provider, connectionString))
            {
                // estimate root path
                // Find: '\RTE\RTE.C' and go back
                if (String.IsNullOrWhiteSpace(pathRoot))
                {
                    pathRoot = (from f in db.Files
                        where f.LeafName == "RTE.C"
                        select f.Name).FirstOrDefault();
                    if (String.IsNullOrWhiteSpace(pathRoot))
                    {
                        MessageBox.Show($"Cant find file 'RTE.C' in\r\n{connectionString} ", "Can't determine root path of source code.");
                        return false;
                    }
                    pathRoot = Path.GetDirectoryName(pathRoot);
                    pathRoot = Directory.GetParent(pathRoot).FullName;

                }
                // Estimates macros which concerns functions
                var macros = (from m in db.CodeItems
                    join file in db.Files on m.FileId equals file.Id
                    where m.Kind == 33 && file.Name.Contains(pathRoot) && (file.LeafName.EndsWith(".h") || file.LeafName.EndsWith(".hpp"))
                    orderby file.Name
                    select new { MacroName = m.Name, FilePath = file.Name, FileName = file.LeafName, m.StartLine, m.StartColumn, m.EndLine, m.EndColumn }).Distinct();

                int step =1;
                int count=0;
                if (backgroundWorker != null)
                {
                    step = macros.Count() / 50;
                    count = step;
                    backgroundWorker.ReportProgress(2);
                }
                _macros.Clear();
                string fileLast = "";
                string[] code = new string[] { "" };
                foreach (var m in macros)
                {
                    if (backgroundWorker != null)
                    {
                        count += 1;
                        if (count % step == 0) backgroundWorker.ReportProgress(count/step);
                    }
                    // get file content if file changed
                    if (fileLast != m.FilePath)
                    {
                        fileLast = m.FilePath;
                        code = File.ReadAllLines(m.FilePath);
                    }
                    string text = code[m.StartLine - 1].Substring((int)m.StartColumn - 1);
                    string[] lText = text.Split(' ');
                    if (lText.Count() == 3)
                    {
                        string key = lText[2];
                        if (!_macros.ContainsKey(key))
                            _macros.Add(key, m.MacroName );
                    }
                }


            }
            return true;
        }
    }
}
