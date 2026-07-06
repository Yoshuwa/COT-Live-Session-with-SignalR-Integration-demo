using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace yoshuwa.Data
{
	public class SelectClauseDictionary : SortedDictionary<string, string>
    {
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private bool _trackAliases;
        
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private List<string> _referencedAliases;
        
        public bool TrackAliases
        {
            get
            {
                return _trackAliases;
            }
            set
            {
                _trackAliases = value;
            }
        }
        
        public List<string> ReferencedAliases
        {
            get
            {
                return _referencedAliases;
            }
            set
            {
                _referencedAliases = value;
            }
        }
        
        public new string this[string name]
        {
            get
            {
                string expression = null;
                if (!(TryGetValue(name.ToLower(), out expression)))
                	expression = "null";
                else
                	if (TrackAliases)
                    {
                        var m = Regex.Match(expression, "^(\'|\"|\\[|`)(?\'Alias\'.+?)(\'|\"|\\]|`)");
                        if (m.Success)
                        {
                            if (ReferencedAliases == null)
                            	ReferencedAliases = new List<string>();
                            var aliasName = m.Groups["Alias"].Value;
                            if (m.Success && !(ReferencedAliases.Contains(aliasName)))
                            	ReferencedAliases.Add(aliasName);
                        }
                    }
                return expression;
            }
            set
            {
                base[name.ToLower()] = value;
            }
        }
        
        public new bool ContainsKey(string name)
        {
            return base.ContainsKey(name.ToLower());
        }
        
        public new void Add(string key, string value)
        {
            base.Add(key.ToLower(), value);
        }
        
        public new bool TryGetValue(string key, out string value)
        {
            return base.TryGetValue(key.ToLower(), out value);
        }
    }
    
    public interface IDataController
    {
        
        ViewPage GetPage(string controller, string view, PageRequest request);
        
        object[] GetListOfValues(string controller, string view, DistinctValueRequest request);
        
        ActionResult Execute(string controller, string view, ActionArgs args);
    }
    
    public interface IAutoCompleteManager
    {
        
        string[] GetCompletionList(string prefixText, int count, string contextKey);
    }
    
    public interface IActionHandler
    {
        
        void BeforeSqlAction(ActionArgs args, ActionResult result);
        
        void AfterSqlAction(ActionArgs args, ActionResult result);
        
        void ExecuteAction(ActionArgs args, ActionResult result);
    }
    
    public interface IRowHandler
    {
        
        bool SupportsNewRow(PageRequest requet);
        
        void NewRow(PageRequest request, ViewPage page, object[] row);
        
        bool SupportsPrepareRow(PageRequest request);
        
        void PrepareRow(PageRequest request, ViewPage page, object[] row);
    }
    
    public interface IDataFilter
    {
        
        void Filter(SortedDictionary<string, object> filter);
    }
    
    public interface IDataFilter2
    {
        
        void Filter(string controller, string view, SortedDictionary<string, object> filter);
        
        void AssignContext(string controller, string view, string lookupContextController, string lookupContextView, string lookupContextFieldName);
    }
    
    public interface IDataEngine
    {
        
        DbDataReader ExecuteReader(PageRequest request);
    }
    
    public interface IPlugIn
    {
        
        ControllerConfiguration Config
        {
            get;
            set;
        }
        
        ControllerConfiguration Create(ControllerConfiguration config);
        
        void PreProcessPageRequest(PageRequest request, ViewPage page);
        
        void ProcessPageRequest(PageRequest request, ViewPage page);
        
        void PreProcessArguments(ActionArgs args, ActionResult result, ViewPage page);
        
        void ProcessArguments(ActionArgs args, ActionResult result, ViewPage page);
    }
    
    public class BusinessObjectParameters : SortedDictionary<string, object>
    {
        
        private string _parameterMarker = null;
        
        public BusinessObjectParameters()
        {
        }
        
        public BusinessObjectParameters(params System.Object[] values)
        {
            Assign(values);
        }
        
        public static BusinessObjectParameters Create(string parameterMarker, params System.Object[] values)
        {
            var paramList = new BusinessObjectParameters();
            paramList._parameterMarker = parameterMarker;
            paramList.Assign(values);
            return paramList;
        }
        
        public void Assign(params System.Object[] values)
        {
            var parameterMarker = _parameterMarker;
            for (var i = 0; (i < values.Length); i++)
            {
                var v = values[i];
                if (v is FieldValue)
                {
                    var fv = ((FieldValue)(v));
                    Add(fv.Name, fv.Value);
                }
                else
                	if (v is BusinessObjectParameters)
                    {
                        var paramList = ((BusinessObjectParameters)(v));
                        foreach (var name in paramList.Keys)
                        	Add(name, paramList[name]);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(parameterMarker))
                        	parameterMarker = SqlStatement.GetParameterMarker(string.Empty);
                        if ((v != null) && (v.GetType().Namespace == null))
                        	foreach (var pi in v.GetType().GetProperties())
                            	Add((parameterMarker + pi.Name), pi.GetValue(v));
                        else
                        	Add((parameterMarker 
                                            + ("p" + i.ToString())), v);
                    }
            }
        }
        
        public string ToWhere()
        {
            var filterExpression = new StringBuilder();
            foreach (var paramName in Keys)
            {
                if (filterExpression.Length > 0)
                	filterExpression.Append("and");
                var v = this[paramName];
                if (DBNull.Value.Equals(v) || (v == null))
                	filterExpression.AppendFormat("({0} is null)", paramName.Substring(1));
                else
                	filterExpression.AppendFormat("({0}={1})", paramName.Substring(1), paramName);
            }
            return filterExpression.ToString();
        }
    }
    
    public interface IBusinessObject
    {
        
        void AssignFilter(string filter, BusinessObjectParameters parameters);
    }
    
    public enum CommandConfigurationType
    {
        
        Select,
        
        Update,
        
        Insert,
        
        Delete,
        
        SelectCount,
        
        SelectDistinct,
        
        SelectAggregates,
        
        SelectFirstLetters,
        
        SelectExisting,
        
        Sync,
        
        None,
    }
}
