﻿using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DocumentFormat.OpenXml.Drawing;
using MariGold.OpenXHTML;

namespace EaServices.Doors
{
    public class HtmlToDocx
    {
        /// <summary>
        /// Convert XHTML to *.docx
        /// </summary>
        /// <param name="docXFile">The file to store the result *.docX document</param>
        /// <param name="xhtml"></param>
        /// <returns></returns>
        public static void Convert(string docXFile, string xhtml)
        {
		
            xhtml = XhtmlFromReqIf(xhtml);
            // write to *.docx
            if (String.IsNullOrWhiteSpace(xhtml)) xhtml = "Empty!!!";
            WordDocument doc = new WordDocument(docXFile);

            var uri = new System.Uri(System.IO.Path.GetDirectoryName(docXFile));
            doc.ImagePath = uri.AbsoluteUri;

            //doc.Process(new HtmlParser("<div>sample text</div>"));
            try
            {
                if (xhtml.Contains("<img"))
                {
                    doc.Process(new HtmlParser(xhtml));
                    doc.Save();
                }
                else
                {
                    doc.Process(new HtmlParser(xhtml));
                    doc.Save();
                }
               

            }
            catch (Exception e)
            {
                MessageBox.Show($@"XHTML:'{xhtml}{Environment.NewLine}{Environment.NewLine}{e}", @"Error converting XHTML to *.docx");
            }

        }
        /// <summary>
        /// Convert xhtml to rtf with Sautin converter
        /// </summary>
        /// <param name="xhtml"></param>
        /// <param name="rtfFile"></param>
        public static void ConvertSautin(string rtfFile, string xhtml )
        {
            try
            {
                xhtml = XhtmlFromReqIf(xhtml);
                // write to *.docx
                if (String.IsNullOrWhiteSpace(xhtml)) xhtml = "Empty!!!";

                // Open the result for demonstation purposes.
                SautinSoft.HtmlToRtf h = new SautinSoft.HtmlToRtf();
                string xhtmlFile = System.IO.Path.GetDirectoryName(rtfFile);
                xhtmlFile = System.IO.Path.Combine(xhtmlFile, "xxxxxx.xhtml");

                //rtfFile = System.IO.Path.GetDirectoryName(rtfFile);
                //rtfFile = System.IO.Path.Combine(rtfFile, "xxxxxx.rtf");


                System.IO.File.WriteAllText(xhtmlFile, xhtml);
                if (h.OpenHtml(xhtmlFile))
                {
                    bool ok;
                    if (xhtml.Contains("<img"))
                    {
                        xhtml = xhtml.Replace(@"type=""image/png""", "");
                        ok = h.ToRtf(rtfFile);
                    }
                    else
                    {
                        ok = h.ToRtf(rtfFile);
                    }
                    if (! ok)
                    {
                        MessageBox.Show($@"XHTML:'{xhtml}{Environment.NewLine}File:{rtfFile}", @"Error0 converting XHTML to *.rtf");
                    }
                }
                else
                {
                    MessageBox.Show($@"XHTML:'{xhtml}{Environment.NewLine}File:{rtfFile}", @"Error1 converting XHTML to *.rtf");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($@"XHTML:'{xhtml}{Environment.NewLine}{Environment.NewLine}{e}", @"Error converting XHTML to *.rtf");
            }
        }


        /// <summary>
        /// Delete XHTML objects from xhtml. Currently only 'img' is supported
        /// </summary>
        /// <param name="xhtml"></param>
        /// <returns></returns>
        public static string DeleteObjects(string xhtml)
        {
            while ( xhtml.Contains("<img") )
            {
                string delOleObject = @"<img.*?</img>";
                Regex regDelOleObject = new Regex(delOleObject);
                Match match = regDelOleObject.Match(xhtml);
                while (match.Success)
                {
                    xhtml = xhtml.Replace(match.Groups[0].Value, "");
                    match = match.NextMatch();

                }
            }

            return xhtml;

        }
        /// <summary>
        /// Make XHTML from ReqIF xhtml. This is:
        /// - Remove namespace
        /// - nested <object for ole object to simple image
        /// - transform <object to <img
        /// </summary>
        /// <param name="xhtml"></param>
        /// <returns></returns>
        public static string XhtmlFromReqIf(string xhtml)
        {
            // remove namespace http://www.w3.org/1999/xhtml
            Regex regNameSpaceXhtml = new Regex(@"xmlns:([^=]*)=""http://www.w3.org/1999/xhtml"">");
            Match match = regNameSpaceXhtml.Match(xhtml);
            if (match.Success == true)
            {
                xhtml = xhtml.Replace($"{match.Groups[1].Value}:", "");
            }

            //<object data="OLE_AB_4e7c971411315592_23_210006b143_2800000149__066872fb-03dd-45cd-85f6-2aaf7cecfaff_OBJECTTEXT_0.ole" type="text/rtf">
            //    <object data="OLE_AB_4e7c971411315592_23_210006b143_2800000149__066872fb-03dd-45cd-85f6-2aaf7cecfaff_OBJECTTEXT_0.png" type="image/png">OLE Object
            //    </object>
            //    </object></div>
            string delOleObject = @"<object data=[^>]*>\s*(<object.*?\s*</object>)\s*</object>";
            Regex regDelOleObject = new Regex(delOleObject);
            match = regDelOleObject.Match(xhtml);
            while (match.Success)
            {
                xhtml = xhtml.Replace(match.Groups[0].Value, match.Groups[1].Value);
               match =  match.NextMatch();

            }


            // change '<object' to '<img' 
            string regex = @"<object.*type=\""([^\""]*)\"">.*</object>";
            Regex regObjectToImg = new Regex(regex);
            match = regObjectToImg.Match(xhtml);
            while (match.Success)
            {
                switch (match.Groups[1].Value)
                {
                      case  "image/png":
                          string found = match.Groups[0].Value;
                          found = found.Replace("<object ", "<img ");
                          found = found.Replace("</object>", "</img>");
                          found = found.Replace(@" data=""", @" src=""");
                          xhtml = xhtml.Replace(match.Groups[0].Value, found);
                          break;
                      // not allowed types
                      default: 
                          xhtml = xhtml.Replace(match.Groups[0].Value, "");
                          break;

                }

                match = match.NextMatch();

            }

            return xhtml;
        }
    }
}