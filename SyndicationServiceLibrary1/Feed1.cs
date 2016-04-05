using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Syndication;
using System.ServiceModel.Web;
using System.Text;
using System.IO;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;

namespace SyndicationServiceLibrary1
{
    public class NationalAccountsReceive : IFeed1
    {
        public SyndicationFeedFormatter CreateFeed()
        {
            // Create a new Syndication Feed.
            SyndicationFeed feed = new SyndicationFeed("EWA Files Receive Log", "A WCF Syndication Feed", null);
            List<SyndicationItem> items = new List<SyndicationItem>();
            items.Count();

            // Category and Description
            feed.Categories.Add(new SyndicationCategory("Validated"));
            
            feed.Description = new TextSyndicationContent("RSS feed for files received through EWA");
            



            // Put all file names in root directory into array.
            string[] SDMXFiles = Directory.GetFiles(ConfigurationManager.AppSettings["EWAPath"], "*.xml");
            
            // Loop thru files
            foreach (string SDMXFile in SDMXFiles)
            {
                string SDMXFileName = SDMXFile.Substring(SDMXFile.LastIndexOf('\\') + 1);
                string SDMXFolderName = SDMXFile.Substring(0, SDMXFile.LastIndexOf('\\') - 1);

                XDocument xDoc = XDocument.Load(SDMXFile);



                // Get all namespaces in SDMX doc
                var nspaces = xDoc.Root.Attributes().
                        Where(a => a.IsNamespaceDeclaration).
                        GroupBy(a => a.Name.Namespace == XNamespace.None ? String.Empty : a.Name.LocalName,
                                a => XNamespace.Get(a.Value)).
                        ToDictionary(g => g.Key,
                                     g => g.First());

                XNamespace xNs_Header = null, xNs_Data = null;

                // Loop thru namespaces, get the ones we need for Header and Data
                foreach (var ns in nspaces)
                {
                    if (ns.Value.ToString().Contains("/message")) xNs_Header = ns.Value;
                    if (ns.Value.ToString().Contains("KeyFamily")) xNs_Data = ns.Value;
                }

                //1.	Prepared date in SDMX header
                //2.	Table code: In SDMX header DataSetID
                //3.	Frequency: first series attribute FREQ
                //4.	Country ISO: First series attribute REF_AREA
                //5.	Validated or not: Look for _VALID_ in file name
                //7.    Sender message notes: Include link to EWA interface
                //8.    Folder name where the file was saved to

                // Put header information into variables
                var headerLines = from header in xDoc.Descendants(xNs_Header + "Header")
                                  select new
                                  {
                                      Prepared = header.Element(xNs_Header + "Prepared").Value,
                                      SenderCode = header.Element(xNs_Header + "Sender").Attribute("id").Value,
                                      ID = header.Element(xNs_Header + "DataSetID").Value
                                  };

                // Get frequency from first series attribute
                bool Validated = SDMXFileName.ToLower().Contains("_valid_");

                // Get frequency from first series attribute
                string Freq = xDoc.Descendants(xNs_Data + "DataSet").Descendants(xNs_Data + "Series").First().Attribute("FREQ").Value;

                // Get frequency from first series attribute
                string Country = xDoc.Descendants(xNs_Data + "DataSet").Descendants(xNs_Data + "Series").First().Attribute("REF_AREA").Value;

                // Get table code from header ID
                string[] ID_Bits = headerLines.First().ID.Split('_');
                string TableCode;
                if (ID_Bits.Count() > 1)
                    TableCode = ID_Bits[1].ToString().Trim();
                else
                    TableCode = headerLines.First().ID;

                // Format the prepared date/time
                string PreparedDate = headerLines.First().Prepared.ToString().Replace('T', ' ').Trim();
                if (PreparedDate.Contains("+")) { 
                    PreparedDate = PreparedDate.Substring(0,PreparedDate.IndexOf('+') ) ; 
                }

                // Build content string
                string Content =
                     "  <b>TableCode:</b>" + TableCode
                    + " \t <b>Frequency:</b>" + Freq
                    + " \t <b>Country:</b>" + Country
                    + " \t <b>Validated:</b>" + Validated
                    + " \t <b>SenderCode:</b>" + headerLines.First().SenderCode;

                Uri fileUri = new Uri("file://" + SDMXFile);

                DateTimeOffset PublishDate=DateTime.MinValue;
                try
                {
                    PublishDate = DateTime.ParseExact(PreparedDate, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    Content += " \t <b>Wrong format prepared date:</b>" + PreparedDate;
                }

                //// Put all info into a new RSS item and add it to the feed
                //item = new SyndicationItem(
                //    "File:" + SDMXFileName
                //    , Content
                //    , file
                //);

                SyndicationItem item = new SyndicationItem();
                if (PublishDate != DateTime.MinValue) item.PublishDate = PublishDate;
                item.Content = new TextSyndicationContent(Content);
                item.Title = new TextSyndicationContent("File:" + SDMXFileName);
                item.Links.Add(new SyndicationLink(fileUri));

                items.Add(item);

                //break;

            }

            feed.Items = items;

            // Return ATOM or RSS based on query string
            // rss -> http://localhost:8732/Design_Time_Addresses/SyndicationServiceLibrary1/Feed1/
            // atom -> http://localhost:8732/Design_Time_Addresses/SyndicationServiceLibrary1/Feed1/?format=atom
            string query = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters["format"];
            
            SyndicationFeedFormatter formatter = null;
            //if (query == "atom")
            //{
            //    formatter = new Atom10FeedFormatter(feed);
            //}
            //else
            //{
                formatter = new Rss20FeedFormatter(feed);
            //}

            return formatter;
        }
    }
}
