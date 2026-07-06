using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;
using System.Web.Security;

namespace yoshuwa.Data
{
	public partial class DataControllerBase
    {
        
        public const int MaximumRssItems = 200;
        
        private static SortedDictionary<string, string> _rowsetTypeMap;
        
        public static SortedDictionary<string, string> RowsetTypeMap
        {
            get
            {
                return _rowsetTypeMap;
            }
        }
        
        private void ExecuteDataExport(ActionArgs args, ActionResult result)
        {
            if (!(string.IsNullOrEmpty(args.CommandArgument)))
            {
                var arguments = args.CommandArgument.Split(',');
                if (arguments.Length > 0)
                {
                    var sameController = (args.Controller == arguments[0]);
                    args.Controller = arguments[0];
                    if (arguments.Length == 1)
                    	args.View = "grid1";
                    else
                    	args.View = arguments[1];
                    if (sameController)
                    	args.SortExpression = null;
                    SelectView(args.Controller, args.View);
                }
            }
            var request = new PageRequest(-1, -1, null, null);
            request.SortExpression = args.SortExpression;
            request.Filter = args.Filter;
            request.ContextKey = null;
            request.PageIndex = 0;
            request.PageSize = Int32.MaxValue;
            request.View = args.View;
            if (args.CommandName.EndsWith("Template"))
            {
                request.PageSize = 0;
                args.CommandName = "ExportCsv";
            }
            // store export data to a temporary file
            var fileName = Path.GetTempFileName();
            var writer = File.CreateText(fileName);
            try
            {
                var page = new ViewPage(request);
                page.ApplyDataFilter(_config.CreateDataFilter(), args.Controller, args.View, null, null, null);
                if (_serverRules == null)
                {
                    _serverRules = _config.CreateBusinessRules();
                    if (_serverRules == null)
                    	_serverRules = CreateBusinessRules();
                }
                _serverRules.Page = page;
                _serverRules.ExecuteServerRules(request, ActionPhase.Before);
                using (var connection = CreateConnection(this))
                {
                    var selectCommand = CreateCommand(connection);
                    if ((selectCommand == null) && _serverRules.EnableResultSet)
                    {
                        PopulatePageFields(page);
                        EnsurePageFields(page, null);
                    }
                    ConfigureCommand(selectCommand, page, CommandConfigurationType.Select, null);
                    var reader = ExecuteResultSetReader(page);
                    if (reader == null)
                    	reader = selectCommand.ExecuteReader();
                    if (args.CommandName.EndsWith("Csv"))
                    	ExportDataAsCsv(page, reader, writer);
                    if (args.CommandName.EndsWith("Rss"))
                    	ExportDataAsRss(page, reader, writer);
                    if (args.CommandName.EndsWith("Rowset"))
                    	ExportDataAsRowset(page, reader, writer);
                    reader.Close();
                }
                _serverRules.ExecuteServerRules(request, ActionPhase.After);
            }
            finally
            {
                writer.Close();
            }
            result.Values.Add(new FieldValue("FileName", null, fileName));
        }
        
        private void ExportDataAsRowset(ViewPage page, DbDataReader reader, StreamWriter writer)
        {
            var s = "uuid:BDC6E3F0-6DA3-11d1-A2A3-00AA00C14882";
            var dt = "uuid:C2F41010-65B3-11d1-A29F-00AA00C14882";
            var rs = "urn:schemas-microsoft-com:rowset";
            var z = "#RowsetSchema";
            var settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            var output = XmlWriter.Create(writer, settings);
            output.WriteStartDocument();
            output.WriteStartElement("xml");
            output.WriteAttributeString("xmlns", "s", null, s);
            output.WriteAttributeString("xmlns", "dt", null, dt);
            output.WriteAttributeString("xmlns", "rs", null, rs);
            output.WriteAttributeString("xmlns", "z", null, z);
            // declare rowset schema
            output.WriteStartElement("Schema", s);
            output.WriteAttributeString("id", "RowsetSchema");
            output.WriteStartElement("ElementType", s);
            output.WriteAttributeString("name", "row");
            output.WriteAttributeString("content", "eltOnly");
            output.WriteAttributeString("CommandTimeout", rs, "60");
            var fields = new List<DataField>();
            foreach (var field in page.Fields)
            	if (!((field.Hidden || (field.OnDemand || (field.Type == "DataView")))) && !(fields.Contains(field)))
                {
                    var aliasField = field;
                    if (!(string.IsNullOrEmpty(field.AliasName)))
                    	aliasField = page.FindField(field.AliasName);
                    fields.Add(aliasField);
                }
            var number = 1;
            foreach (var field in fields)
            {
                field.NormalizeDataFormatString();
                output.WriteStartElement("AttributeType", s);
                output.WriteAttributeString("name", field.Name);
                output.WriteAttributeString("number", rs, number.ToString());
                output.WriteAttributeString("nullable", rs, "true");
                output.WriteAttributeString("name", rs, field.Label);
                output.WriteStartElement("datatype", s);
                var type = RowsetTypeMap[field.Type];
                string dbType = null;
                if ("{0:c}".Equals(field.DataFormatString, StringComparison.CurrentCultureIgnoreCase))
                	dbType = "currency";
                else
                	if (!(string.IsNullOrEmpty(field.DataFormatString)) && field.Type != "DateTime")
                    	type = "string";
                output.WriteAttributeString("type", dt, type);
                output.WriteAttributeString("dbtype", rs, dbType);
                output.WriteEndElement();
                output.WriteEndElement();
                number++;
            }
            output.WriteStartElement("extends", s);
            output.WriteAttributeString("type", "rs:rowbase");
            output.WriteEndElement();
            output.WriteEndElement();
            output.WriteEndElement();
            // output rowset data
            output.WriteStartElement("data", rs);
            while (reader.Read())
            {
                output.WriteStartElement("row", z);
                foreach (var field in fields)
                {
                    var v = reader[field.Name];
                    if (!(DBNull.Value.Equals(v)))
                    	if (!(string.IsNullOrEmpty(field.DataFormatString)) && !(((field.DataFormatString == "{0:d}") || (field.DataFormatString == "{0:c}"))))
                        	output.WriteAttributeString(field.Name, string.Format(field.DataFormatString, v));
                        else
                        	if (field.Type == "DateTime")
                            	output.WriteAttributeString(field.Name, ((DateTime)(v)).ToString("s"));
                            else
                            	output.WriteAttributeString(field.Name, v.ToString());
                }
                output.WriteEndElement();
            }
            output.WriteEndElement();
            output.WriteEndElement();
            output.WriteEndDocument();
            output.Close();
        }
        
        private void ExportDataAsRss(ViewPage page, DbDataReader reader, StreamWriter writer)
        {
            var appPath = Regex.Replace(HttpContext.Current.Request.Url.AbsoluteUri, "^(.+)Export.ashx.+$", "$1", RegexOptions.IgnoreCase);
            var settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            var output = XmlWriter.Create(writer, settings);
            output.WriteStartDocument();
            output.WriteStartElement("rss");
            output.WriteAttributeString("version", "2.0");
            output.WriteStartElement("channel");
            output.WriteElementString("title", ((string)(_view.Evaluate("string(concat(/c:dataController/@label, \' | \',  @label))", Resolver))));
            output.WriteElementString("lastBuildDate", DateTime.Now.ToString("r"));
            output.WriteElementString("language", System.Threading.Thread.CurrentThread.CurrentCulture.Name.ToLower());
            var rowCount = 0;
            while ((rowCount < MaximumRssItems) && reader.Read())
            {
                output.WriteStartElement("item");
                var hasTitle = false;
                var hasPubDate = false;
                var desc = new StringBuilder();
                for (var i = 0; (i < page.Fields.Count); i++)
                {
                    var field = page.Fields[i];
                    if (!field.Hidden && field.Type != "DataView")
                    {
                        if (rowCount == 0)
                        	field.NormalizeDataFormatString();
                        if (!(string.IsNullOrEmpty(field.AliasName)))
                        	field = page.FindField(field.AliasName);
                        var text = string.Empty;
                        var v = reader[field.Name];
                        if (!(DBNull.Value.Equals(v)))
                        	if (!(string.IsNullOrEmpty(field.DataFormatString)))
                            	text = string.Format(field.DataFormatString, v);
                            else
                            	text = Convert.ToString(v);
                        if (!hasPubDate && (field.Type == "DateTime"))
                        {
                            hasPubDate = true;
                            if (!(string.IsNullOrEmpty(text)))
                            	output.WriteElementString("pubDate", ((DateTime)(reader[field.Name])).ToString("r"));
                        }
                        if (!hasTitle)
                        {
                            hasTitle = true;
                            output.WriteElementString("title", text);
                            var link = new StringBuilder();
                            link.Append(_config.Evaluate("string(/c:dataController/@name)"));
                            foreach (var pkf in page.Fields)
                            	if (pkf.IsPrimaryKey)
                                	link.Append(string.Format("&{0}={1}", pkf.Name, reader[pkf.Name]));
                            var itemGuid = string.Format("{0}Details.aspx?l={1}", appPath, HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.Default.GetBytes(link.ToString()))));
                            output.WriteElementString("link", itemGuid);
                            output.WriteElementString("guid", itemGuid);
                        }
                        else
                        	if (!(string.IsNullOrEmpty(field.OnDemandHandler)) && (field.OnDemandStyle == OnDemandDisplayStyle.Thumbnail))
                            {
                                if (text.Equals("1"))
                                {
                                    desc.AppendFormat("{0}:<br /><img src=\"{1}Blob.ashx?{2}=t", HttpUtility.HtmlEncode(field.Label), appPath, field.OnDemandHandler);
                                    foreach (var f in page.Fields)
                                    	if (f.IsPrimaryKey)
                                        {
                                            desc.Append("|");
                                            desc.Append(reader[f.Name]);
                                        }
                                    desc.Append("\" style=\"width:92px;height:71px;\"/><br />");
                                }
                            }
                            else
                            	desc.AppendFormat("{0}: {1}<br />", HttpUtility.HtmlEncode(field.Label), HttpUtility.HtmlEncode(text));
                    }
                }
                output.WriteStartElement("description");
                output.WriteCData(string.Format("<span style=\\\"font-size:small;\\\">{0}</span>", desc.ToString()));
                output.WriteEndElement();
                output.WriteEndElement();
                rowCount++;
            }
            output.WriteEndElement();
            output.WriteEndElement();
            output.WriteEndDocument();
            output.Close();
        }
        
        private void ExportDataAsCsv(ViewPage page, DbDataReader reader, StreamWriter writer)
        {
            var firstField = true;
            for (var i = 0; (i < page.Fields.Count); i++)
            {
                var field = page.Fields[i];
                if (!field.Hidden && (field.Type != "DataView"))
                {
                    if (firstField)
                    	firstField = false;
                    else
                    	writer.Write(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                    if (!(string.IsNullOrEmpty(field.AliasName)))
                    	field = page.FindField(field.AliasName);
                    writer.Write("\"{0}\"", field.Label.Replace("\"", "\"\""));
                }
                field.NormalizeDataFormatString();
            }
            writer.WriteLine();
            while (reader.Read())
            {
                firstField = true;
                for (var j = 0; (j < page.Fields.Count); j++)
                {
                    var field = page.Fields[j];
                    if (!field.Hidden && (field.Type != "DataView"))
                    {
                        if (firstField)
                        	firstField = false;
                        else
                        	writer.Write(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                        if (!(string.IsNullOrEmpty(field.AliasName)))
                        	field = page.FindField(field.AliasName);
                        var text = string.Empty;
                        var v = reader[field.Name];
                        if (!(DBNull.Value.Equals(v)))
                        {
                            if (!(string.IsNullOrEmpty(field.DataFormatString)))
                            	text = string.Format(field.DataFormatString, v);
                            else
                            	text = Convert.ToString(v);
                            writer.Write("\"{0}\"", text.Replace("\"", "\"\""));
                        }
                        else
                        	writer.Write("\"\"");
                    }
                }
                writer.WriteLine();
            }
        }
    }
}
