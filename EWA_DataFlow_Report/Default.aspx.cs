using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data.SqlClient;
using System.Xml;
using System.Text;
using System.Configuration;
using System.IO;
using System.Xml.Linq;
using System.Linq;

namespace EWA_DataFlow_Report
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/rss+xml";
            //Response.AppendHeader("Refresh", "120");

            XmlTextWriter objX = new XmlTextWriter(Response.OutputStream, Encoding.UTF8);
            objX.WriteStartDocument();
            objX.WriteStartElement("rss");
            objX.WriteAttributeString("version", "2.0");

            objX.WriteAttributeString("xmlns:stats", "http://tempuri.org");

            objX.WriteStartElement("channel");

            objX.WriteElementString("title", "EWA DataFlow Report");

            objX.WriteElementString("stats:sender_name", "Sender name");
            objX.WriteElementString("stats:frequency", "Frequency of data");
            objX.WriteElementString("stats:table_code", "Table code");
            objX.WriteElementString("stats:ref_area", "Reference area");
            objX.WriteElementString("stats:dataset", "Dataset");
            objX.WriteElementString("stats:csv_file", "CSV filepath");
            objX.WriteElementString("stats:prepared_date", "Prepared date");
            objX.WriteElementString("stats:last_modified_date", "Last modified date");
            objX.WriteElementString("stats:COMMENT_DSET", "Comment dataset");
            objX.WriteElementString("stats:COMMENT_TS_FIRST", "Comments series");

            //objX.WriteElementString("stats:COMMENT_OBS", "Comments obs");
            
            objX.WriteElementString("description", "Files that arrive in EWA appear here");
            objX.WriteElementString("language", "en-us");
            objX.WriteElementString("ttl", "60");
            objX.WriteElementString("lastBuildDate", String.Format("{0:R}", DateTime.Now));

            int FileCreationDateDaysLimit = 0;
            int FileCountLimit = 0;

            if (Request["FileCreationDateDaysLimit"] != null)
                int.TryParse(Request["FileCreationDateDaysLimit"].ToString(), out FileCreationDateDaysLimit);

            if (Request["FileCountLimit"] != null)
                int.TryParse(Request["FileCountLimit"].ToString(), out FileCountLimit);
            
            //FileCountLimit = Convert.ToInt32(ConfigurationManager.AppSettings["FileCountLimit"]);

            // Put all file names in root directory into array.
            var orderedFiles = new System.IO.DirectoryInfo(ConfigurationManager.AppSettings["EWAPath"])
                       .GetFiles("*.*")
                       .Where(f => 
                        (FileCreationDateDaysLimit > 0 && f.CreationTime >= DateTime.Now.AddDays(-FileCreationDateDaysLimit) )
                        || (FileCreationDateDaysLimit == 0)
                       )
                       .OrderByDescending(x => x.CreationTime);

            //string[] extensions = new[] { ".jpg", ".tiff", ".bmp" };

            int i = 0;

            // Loop thru files
            foreach (System.IO.FileInfo SDMXFile in orderedFiles)
            {
                // Ignore zipped and non-XML files.  Allow files that are XML but with wrong file extension.
                if (SDMXFile.Extension == ".7z" || SDMXFile.Extension == ".zip" || SDMXFile.Extension == ".csv"
                    || SDMXFile.Extension.StartsWith(".xls") || SDMXFile.Extension.StartsWith(".doc"))
                    continue;

                i++;
                //string FileName = SDMXFile.Substring(SDMXFile.LastIndexOf('\\') + 1);
                string FileName = SDMXFile.Name;

                // only process information for SDMX files
                objX.WriteStartElement("item");

                // if SDMX file
                try
                {
                    string[] ID_Bits = null;
                    string TableCode = "";
                    string DSD = "";
                    string SenderCode = "";
                    string SenderName = "";
                    DateTime PublishDate = DateTime.MinValue;
                    string PreparedDate = "", ReportDate = "";
                    string Content = "";
                    string CSVFile = "";
                    string sError = "";
                    string Freq = "";
                    string Country = "";
                    bool bInHeader = false;
                    bool bInDataSet = false;

                    string CommentDataSet = "", CommentSeriesFirst = "";

                    bool Validated = FileName.ToLower().Contains("_valid_");

                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.IgnoreWhitespace = true;
                    settings.IgnoreProcessingInstructions = true;
                    settings.IgnoreComments = true;

                    XmlReader xmlReader = XmlReader.Create(SDMXFile.FullName, settings);

                    // Read each line in the file
                    while (xmlReader.Read())
                    {
                        //CommentsSeries = new List<string>();
                        //CommentsObs = new List<string>();
                        //string CommentSeries = "", CommentObs = "";

                        if (xmlReader.LocalName == "Header" && xmlReader.NodeType == XmlNodeType.Element) bInHeader = true;
                        if (xmlReader.LocalName == "DataSet" && xmlReader.NodeType == XmlNodeType.Element)
                        {
                            bInHeader = false;
                            bInDataSet = true;
                        }

                        // If we're in the header
                        if (bInHeader && xmlReader.NodeType == XmlNodeType.Element)
                        {
                            // Get table code from header DataSetID or ID
                            if (xmlReader.LocalName == "DataSetID")
                            {
                                ID_Bits = xmlReader.ReadString().Split('_');
                            }
                            if (ID_Bits == null && xmlReader.LocalName == "ID")
                            {
                                ID_Bits = xmlReader.ReadString().Split('_');
                            }

                            if (xmlReader.LocalName == "Sender")
                            {
                                SenderCode += xmlReader.GetAttribute("id");
                            }
                            if (xmlReader.LocalName == "Name")
                            {
                                SenderName += xmlReader.ReadString();
                            }
                            if (xmlReader.LocalName == "Prepared")
                            {
                                try
                                {

                                    PreparedDate += xmlReader.ReadString();

                                    PreparedDate = PreparedDate.Replace('T', ' ').Trim();
                                    PreparedDate = PreparedDate.Substring(0, 16);

                                    PublishDate = DateTime.ParseExact(PreparedDate, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

                                    ReportDate = PublishDate.Year + "-" + PublishDate.Month.ToString("D2") + "-" + PublishDate.Day.ToString("D2") + " " + PublishDate.Hour.ToString("D2") + ":" + PublishDate.Minute.ToString("D2") + ":" + PublishDate.Second.ToString("D2");
                                }
                                catch (Exception ex)
                                {
                                    sError = "; Wrong format prepared date:" + PreparedDate;
                                }
                            }
                        }
                        // If we're not in the header
                        else if (bInDataSet && xmlReader.NodeType == XmlNodeType.Element)
                        {
                            //COMMENT_DSET
                            if (xmlReader.LocalName == "DataSet")
                            {
                                CommentDataSet = xmlReader.GetAttribute("COMMENT_DSET");
                            }

                            if (xmlReader.LocalName == "Series")
                            {
                                Freq = xmlReader.GetAttribute("FREQ");
                                Country = xmlReader.GetAttribute("REF_AREA");

                                CommentSeriesFirst = xmlReader.GetAttribute("COMMENT_TS");
                                //if (CommentSeries != null && CommentSeries != "") CommentsSeries.Add(CommentSeries);

                                break; // Stop reading the file
                            }
                        }
                    } // end while loop on file lines

                    xmlReader.Close();

                    // Cleanup TableCode and DSD value
                    if (ID_Bits != null)
                    {
                        if (ID_Bits.Count() > 1)
                        {
                            TableCode = ID_Bits[1].ToString().Trim();
                            DSD = ID_Bits[0].ToString().Trim();
                        }
                        else
                            TableCode = ID_Bits[0];
                    }

                    //// Build content string
                    Content =
                        "TableCode:" + TableCode
                       + "; Frequency:" + Freq
                       + "; Country:" + Country
                       + "; Validated:" + Validated
                       + "; DSD:" + DSD
                       + "; SenderCode:" + SenderCode
                       + "; SenderName:" + SenderName;

                    if (sError != "") Content += sError;


                    // Make CSV file name
                    if (Validated) CSVFile = ConfigurationManager.AppSettings["CSVPath"] + FileName.Replace(".xml", ".csv");

                    // Write RSS object

                    objX.WriteElementString("stats:dataset", DSD);

                    objX.WriteElementString("stats:frequency", Freq);

                    objX.WriteElementString("stats:table_code", TableCode);

                    objX.WriteElementString("stats:ref_area", Country);

                    objX.WriteElementString("title", FileName);

                    objX.WriteElementString("stats:prepared_date", ReportDate);

                    objX.WriteElementString("stats:last_modified_date", SDMXFile.LastWriteTime.ToShortDateString() + " " + SDMXFile.LastWriteTime.ToShortTimeString());

                    if (PublishDate != DateTime.MinValue) objX.WriteElementString("pubDate", String.Format("{0:R}", PublishDate));

                    objX.WriteElementString("author", SenderCode);

                    objX.WriteElementString("stats:sender_name", SenderName);

                    objX.WriteStartElement("category");
                    if (Validated)
                    {
                        objX.WriteString("Validated");
                    }
                    else
                    {
                        objX.WriteString("Not validated");
                    }
                    objX.WriteEndElement(); // end category

                    objX.WriteElementString("stats:csv_file", CSVFile);

                    objX.WriteElementString("enclosure", SDMXFile.FullName);

                    objX.WriteElementString("stats:COMMENT_DSET", CommentDataSet);
                    objX.WriteElementString("stats:COMMENT_TS_FIRST", CommentSeriesFirst);
                    
                    //objX.WriteElementString("stats:COMMENT_TS", CommentsSeries);
                    //objX.WriteElementString("stats:COMMENT_OBS", CommentsObs);
                }
                catch (Exception ex)
                {
                    objX.WriteElementString("title", FileName + " is not valid SDMX");
                    objX.WriteElementString("author", "");
                    objX.WriteStartElement("category");
                    objX.WriteString("Invalid");
                    objX.WriteEndElement(); // end category
                }

                objX.WriteEndElement(); // end item

                // If the no of files to process is not limited by the request parameters, limit it by the file count limit
                if (FileCreationDateDaysLimit == 0 && i > FileCountLimit) break;

            } // loop

            objX.WriteEndElement();
            objX.WriteEndElement();

            objX.WriteEndDocument();

            objX.Flush();
            objX.Close();
            Response.End();
        }
    }
}