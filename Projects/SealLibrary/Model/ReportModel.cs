﻿//
// Copyright 2015 (c) Seal Report, Eric Pfirsch (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. http://www.apache.org/licenses/LICENSE-2.0
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RazorEngine;
using System.Data;
using System.Xml.Serialization;
using Seal.Converter;
using Seal.Helpers;
using System.ComponentModel;
using System.Windows.Forms.DataVisualization.Charting;
using System.Data.OleDb;
using System.Drawing.Design;
using DynamicTypeDescriptor;
using Seal.Forms;
using System.Threading;
using RazorEngine.Templating;
using System.Diagnostics;
using System.Data.Common;
using System.Data.Odbc;
using System.IO;

namespace Seal.Model
{
    public class ReportModel : ReportComponent
    {
        public static string DefaultClause = "<Default Clause>";


        #region Editor
        protected override void UpdateEditorAttributes()
        {
            if (_dctd != null)
            {
                //Disable all properties
                foreach (var property in Properties) property.SetIsBrowsable(false);
                //Then enable
                GetProperty("SourceGUID").SetIsBrowsable(true);
                GetProperty("ConnectionGUID").SetIsBrowsable(true);

                GetProperty("PreLoadScript").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("LoadScript").SetIsBrowsable(true);
                GetProperty("FinalScript").SetIsBrowsable(true);
                if (Source.IsNoSQL)
                {
                    GetProperty("LoadScript").SetDisplayName("Load Script");
                    GetProperty("LoadScript").SetDescription("The Razor Script used to load the data in the table. If empty, the load script defined in the master table is used.");
                }
                else
                {
                    GetProperty("LoadScript").SetDisplayName("Post Load Script");
                    GetProperty("LoadScript").SetDescription("Optional Razor Script to modify the result table of the model just after the database load.");
                }

                GetProperty("SqlSelect").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("SqlFrom").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("SqlGroupBy").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("SqlOrderBy").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("SqlEditor").SetIsBrowsable(!Source.IsNoSQL);

                GetProperty("PreSQL").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("PostSQL").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("IgnorePrePostError").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("BuildTimeout").SetIsBrowsable(!Source.IsNoSQL);

                GetProperty("ForceJoinTableGUID").SetIsBrowsable(!Source.IsNoSQL);
                GetProperty("AvoidJoinTableGUID").SetIsBrowsable(!Source.IsNoSQL);

                GetProperty("SqlEditor").SetIsReadOnly(true);

                TypeDescriptor.Refresh(this);
            }
        }
        #endregion

        public static ReportModel Create()
        {
            return new ReportModel() { GUID = Guid.NewGuid().ToString() };
        }


        string _sourceGUID;
        [Category("Model Definition"), DisplayName("Source"), Description("The source used to build the model."), Id(1, 1)]
        [TypeConverter(typeof(MetaSourceConverter))]
        public string SourceGUID
        {
            get { return _sourceGUID; }
            set { _sourceGUID = value; }
        }

        protected string _connectionGUID = ReportSource.DefaultReportConnectionGUID;
        [DisplayName("Connection"), Description("The connection used to build the model."), Category("Model Definition"), Id(2, 1)]
        [TypeConverter(typeof(SourceConnectionConverter))]
        public string ConnectionGUID
        {
            get
            {
                if (_connectionGUID != ReportSource.DefaultReportConnectionGUID && _connectionGUID != ReportSource.DefaultRepositoryConnectionGUID)
                {
                    if (Source != null && !Source.Connections.Exists(i => i.GUID == _connectionGUID) && !Source.TempConnections.Exists(i => i.GUID == _connectionGUID)) _connectionGUID = ReportSource.DefaultReportConnectionGUID;
                }
                return _connectionGUID;
            }
            set { _connectionGUID = value; }
        }

        [XmlIgnore]
        public MetaConnection Connection
        {
            get
            {
                MetaConnection result = Source.Connection;
                if (_connectionGUID == ReportSource.DefaultReportConnectionGUID) result = Source.Connection;
                else if (_connectionGUID == ReportSource.DefaultRepositoryConnectionGUID) result = Source.RepositoryConnection;
                else result = Source.Connections.FirstOrDefault(i => i.GUID == _connectionGUID);
                if (result == null && Source.Connections.Count > 0)
                {
                    result = Source.Connections[0];
                }
                return result;
            }
        }

        string _preLoadScript;
        [Category("Model Definition"), DisplayName("Pre Load Script"), Description("Optional Razor Script to modify the result table of the model just before the database load."), Id(3, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
        public string PreLoadScript
        {
            get { return _preLoadScript; }
            set { _preLoadScript = value; }
        }

        string _loadScript;
        [Category("Model Definition"), DisplayName("Load Script"), Description("The Razor Script used to load the data in the table. If empty, the load script defined in the master table is used."), Id(4, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
        public string LoadScript
        {
            get { return _loadScript; }
            set { _loadScript = value; }
        }

        string _finalScript;
        [Category("Model Definition"), DisplayName("Final Script"), Description("Optional Razor Script to modify the model after its generation."), Id(5, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
        public string FinalScript
        {
            get { return _finalScript; }
            set { _finalScript = value; }
        }

        [XmlIgnore]
        [Category("SQL"), DisplayName("SQL Statement"), Id(2, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string SqlEditor
        {
            get { return "<Expand to view SQL>"; }
            set { _sql = value; }
        }

        string _sqlSelect;
        [Category("SQL"), DisplayName("Select Clause"), Description("If not empty, overwrite the SELECT clause in the generated SQL statement."), Id(3, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string SqlSelect
        {
            get { return (_sqlSelect == DefaultClause) ? "" : _sqlSelect; }
            set { _sqlSelect = value; }
        }

        string _sqlFrom;
        [Category("SQL"), DisplayName("From Clause"), Description("If not empty, overwrite the FROM clause in the generated SQL statement."), Id(4, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string SqlFrom
        {
            get { return (_sqlFrom == DefaultClause) ? "" : _sqlFrom; }
            set { _sqlFrom = value; }
        }

        string _sqlGroupBy;
        [Category("SQL"), DisplayName("Group By Clause"), Description("If not empty, overwrite the GROUP BY clause in the generated SQL statement."), Id(5, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string SqlGroupBy
        {
            get { return (_sqlGroupBy == DefaultClause) ? "" : _sqlGroupBy; }
            set { _sqlGroupBy = value; }
        }

        string _sqlOrderBy;
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        [Category("SQL"), DisplayName("Order By Clause"), Description("If not empty, overwrite the ORDER BY clause in the generated SQL statement."), Id(6, 2)]
        public string SqlOrderBy
        {
            get { return (_sqlOrderBy == DefaultClause) ? "" : _sqlOrderBy; }
            set { _sqlOrderBy = value; }
        }

        string _preSQL;
        [Category("SQL"), DisplayName("Pre SQL Statement"), Description("SQL Statement executed before the main query. The statement may contain Razor script if it starts with '@'."), Id(7, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string PreSQL
        {
            get { return _preSQL; }
            set { _preSQL = value; }
        }

        string _postSQL;
        [Category("SQL"), DisplayName("Post SQL Statement"), Description("SQL Statement executed after the main query. The statement may contain Razor script if it starts with '@'."), Id(8, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
        public string PostSQL
        {
            get { return _postSQL; }
            set { _postSQL = value; }
        }

        bool _ignorePrePostError;
        [Category("SQL"), DisplayName("Ignore Pre and Post SQL Errors"), Description("If true, errors occuring during the Pre or Post SQL statements are ignored and the execution continues."), Id(9, 2)]
        public bool IgnorePrePostError
        {
            get { return _ignorePrePostError; }
            set { _ignorePrePostError = value; }
        }

        int _buildTimout = 4000;
        [Category("SQL"), DisplayName("Build Timeout (ms)"), Description("Timeout in milliseconds to set the maximum duration used to build the SQL (may be used if many joins are defined)."), Id(10, 2)]
        public int BuildTimeout
        {
            get { return _buildTimout; }
            set { _buildTimout = value; }
        }

        private string _forceJoinTableGUID;
        [Category("Join Preferences"), DisplayName("Join table to use"), Description("If not empty, the dynamic SQL joins used to perform the query will be chosen to use the table specified."), Id(2, 3)]
        [TypeConverter(typeof(SourceTableConverter))]
        public string ForceJoinTableGUID
        {
            get { return _forceJoinTableGUID; }
            set { _forceJoinTableGUID = value; }
        }
        public MetaTable ForceJoinTable
        {
            get { return Source.MetaData.Tables.FirstOrDefault(i => i.GUID == _forceJoinTableGUID); }
        }

        private string _avoidTableGUID;
        [Category("Join Preferences"), DisplayName("Join table to avoid"), Description("If not empty, the dynamic SQL joins used to perform the query will be chosen to avoid the table specified."), Id(3, 3)]
        [TypeConverter(typeof(SourceTableConverter))]
        public string AvoidJoinTableGUID
        {
            get { return _avoidTableGUID; }
            set { _avoidTableGUID = value; }
        }
        public MetaTable AvoidJoinTable
        {
            get { return Source.MetaData.Tables.FirstOrDefault(i => i.GUID == _avoidTableGUID); }
        }

        [XmlIgnore]
        public ReportSource Source
        {
            get
            {
                ReportSource result = _report.Sources.FirstOrDefault(i => i.GUID == _sourceGUID);
                if (result == null)
                {
                    if (_report.Sources.Count == 0) throw new Exception("This report has no source defined");
                    result = _report.Sources[0];
                    _sourceGUID = result.GUID;
                }
                return result;
            }
        }

        [XmlIgnore]
        public bool HasSerie
        {
            get
            {
                return Elements.Exists(i => i.SerieDefinition == SerieDefinition.Axis && (i.PivotPosition == PivotPosition.Column || i.PivotPosition == PivotPosition.Row)) && Elements.Exists(i => i.SerieDefinition != SerieDefinition.None && i.PivotPosition == PivotPosition.Data);
            }
        }

        [XmlIgnore]
        public bool HasMicrosoftSerie
        {
            get
            {
                return Elements.Exists(i => i.SerieDefinition == SerieDefinition.Axis && (i.PivotPosition == PivotPosition.Column || i.PivotPosition == PivotPosition.Row)) && Elements.Exists(i => i.SerieDefinition == SerieDefinition.Serie && i.PivotPosition == PivotPosition.Data);
            }
        }

        [XmlIgnore]
        public bool HasNVD3Serie
        {
            get
            {
                return Elements.Exists(i => i.SerieDefinition == SerieDefinition.Axis && (i.PivotPosition == PivotPosition.Column || i.PivotPosition == PivotPosition.Row)) && Elements.Exists(i => i.SerieDefinition == SerieDefinition.NVD3Serie && i.PivotPosition == PivotPosition.Data);
            }
        }

        [XmlIgnore]
        public bool HasTotals
        {
            get
            {
                return Elements.Exists(i => i.PivotPosition == PivotPosition.Data && i.ShowTotal != ShowTotal.No);
            }
        }

        [XmlIgnore]
        public bool HasCellScript
        {
            get
            {
                return Elements.Exists(i => !string.IsNullOrWhiteSpace(i.CellScript));
            }
        }
        //Elements selected
        private List<ReportElement> _elements = new List<ReportElement>();
        public List<ReportElement> Elements
        {
            get { return _elements; }
            set { _elements = value; }
        }

        public IEnumerable<ReportElement> GetElements(PivotPosition position)
        {
            return Elements.Where(i => i.PivotPosition == position);
        }

        public IEnumerable<ReportElement> GetXElements(AxisType xAxisType)
        {
            return Elements.Where(i => i.XAxisType == xAxisType && (i.PivotPosition == PivotPosition.Row || i.PivotPosition == PivotPosition.Column) && (i.SerieDefinition == SerieDefinition.Axis));
        }

        public IEnumerable<ReportElement> GetSplitterElements(AxisType xAxisType)
        {
            return Elements.Where(i => ((i.XAxisType == xAxisType && i.SerieDefinition == SerieDefinition.Splitter) || i.SerieDefinition == SerieDefinition.SplitterBoth) && (i.PivotPosition == PivotPosition.Row || i.PivotPosition == PivotPosition.Column));
        }

        public void CheckSortOrders()
        {
            //Force sort orders on Page elements
            for (int i = 0; i < GetElements(PivotPosition.Page).Count(); i++)
            {
                var element = GetElements(PivotPosition.Page).ElementAt(i);
                if (string.IsNullOrEmpty(element.SortOrder) || element.SortOrder == SortOrderConverter.kNoSortKeyword) element.SortOrder = string.Format("{0} Ascendant", i + 1);
            }
        }

        public void CheckSeries()
        {
            foreach (var element in Elements)
            {
                if (element.PivotPosition == PivotPosition.Page) element.SerieDefinition = SerieDefinition.None;
                else if ((element.PivotPosition == PivotPosition.Row || element.PivotPosition == PivotPosition.Column) && element.IsSerie) element.SerieDefinition = SerieDefinition.None;
                else if (element.PivotPosition == PivotPosition.Data && !element.IsSerie) element.SerieDefinition = SerieDefinition.None;
            }
        }

        //Restrictions
        private string _restriction;
        public string Restriction
        {
            get { return _restriction; }
            set { _restriction = value; }
        }

        private List<ReportRestriction> _restrictions = new List<ReportRestriction>();
        public List<ReportRestriction> Restrictions
        {
            get { return _restrictions; }
            set { _restrictions = value; }
        }


        //Aggregate Restrictions
        private string _aggregateRestriction;
        public string AggregateRestriction
        {
            get { return _aggregateRestriction; }
            set { _aggregateRestriction = value; }
        }

        private List<ReportRestriction> _aggregateRestrictions = new List<ReportRestriction>();
        public List<ReportRestriction> AggregateRestrictions
        {
            get { return _aggregateRestrictions; }
            set { _aggregateRestrictions = value; }
        }


        //Execution
        private string _sql;
        [XmlIgnore]
        public string Sql
        {
            get { return _sql; }
            set { _sql = value; }
        }

        List<MetaTable> _fromTables;
        [XmlIgnore]
        public List<MetaTable> FromTables
        {
            get { return _fromTables; }
        }

        [XmlIgnore]
        public string RestrictionText;

        [XmlIgnore]
        public DateTime ExecutionDate;
        [XmlIgnore]
        public int ExecutionDuration;
        [XmlIgnore]
        public DataTable ResultTable;

        [XmlIgnore]
        public ResultTable SummaryTable;
        [XmlIgnore]
        public List<ResultPage> Pages = new List<ResultPage>();
        [XmlIgnore]
        public string ExecutionError = "";
        [XmlIgnore]
        public List<ReportRestriction> ExecutionRestrictions
        {
            get
            {
                List<ReportRestriction> result = new List<ReportRestriction>();
                foreach (ReportRestriction restriction in Restrictions)
                {
                    ReportRestriction newRestriction = restriction;
                    if (Report.ForOutput && Report.OutputToExecute.UseCustomRestrictions)
                    {
                        newRestriction = Report.OutputToExecute.Restrictions.FirstOrDefault(i => i.GUID == restriction.GUID);
                        if (newRestriction == null) newRestriction = restriction;
                    }
                    result.Add(newRestriction);
                }
                return result;
            }
        }

        [XmlIgnore]
        public List<ReportRestriction> ExecutionAggregateRestrictions
        {
            get
            {
                List<ReportRestriction> result = new List<ReportRestriction>();
                foreach (ReportRestriction restriction in AggregateRestrictions)
                {
                    ReportRestriction newRestriction = restriction;
                    if (Report.ForOutput && Report.OutputToExecute.UseCustomRestrictions)
                    {
                        newRestriction = Report.OutputToExecute.Restrictions.FirstOrDefault(i => i.GUID == restriction.GUID);
                        if (newRestriction == null) newRestriction = restriction;
                    }
                    result.Add(newRestriction);
                }
                return result;
            }
        }


        [XmlIgnore]
        public string execSelect = "";
        [XmlIgnore]
        public StringBuilder execSelectClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execFromClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execWhereClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execOrderByClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execOrderByNameClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execGroupByClause = new StringBuilder();
        [XmlIgnore]
        public StringBuilder execHavingClause = new StringBuilder();

        [XmlIgnore]
        public Object Tag;

        public void InitReferences()
        {
            if (Source.MetaData == null) return;

            foreach (var element in Elements)
            {
                element.SetSourceReference(Source);
                element.Model = this;
            }

            foreach (var restriction in Restrictions)
            {
                restriction.SetSourceReference(Source);
                restriction.Model = this;
            }

            foreach (var restriction in AggregateRestrictions)
            {
                restriction.SetSourceReference(Source);
                restriction.Model = this;
            }

            //clean up lost elements...
            ClearLostElements();

            if (AvoidJoinTable == null) _avoidTableGUID = "";
            if (ForceJoinTable == null) _forceJoinTableGUID = "";
        }

        public void ClearLostElements()
        {
            if (Source.MetaData == null) return;

            int i = Elements.Count;
            while (--i >= 0)
            {
                if (Elements[i].MetaColumn == null) Elements.RemoveAt(i);
            }
            i = Restrictions.Count;
            while (--i >= 0)
            {
                if (string.IsNullOrWhiteSpace(Restriction)) Restrictions.RemoveAt(i);
                else if (Restrictions[i].MetaColumn == null)
                {
                    if (Restriction != null) Restriction = Restriction.Replace(Restrictions[i].GUID, "(Warning) Restriction lost: " + Restrictions[i].Name);
                    Restrictions.RemoveAt(i);
                }
                else if (!Restriction.Contains(Restrictions[i].GUID)) Restrictions.RemoveAt(i);
            }
            i = AggregateRestrictions.Count;
            while (--i >= 0)
            {
                if (string.IsNullOrWhiteSpace(AggregateRestriction)) AggregateRestrictions.RemoveAt(i);
                if (AggregateRestrictions[i].MetaColumn == null)
                {
                    if (AggregateRestriction != null) AggregateRestriction = AggregateRestriction.Replace(AggregateRestrictions[i].GUID, "(Warning) Restriction lost: " + AggregateRestrictions[i].Name);
                    AggregateRestrictions.RemoveAt(i);
                }
                else if (!AggregateRestriction.Contains(AggregateRestrictions[i].GUID)) AggregateRestrictions.RemoveAt(i);
            }
        }

        public void SetColumnsName()
        {
            int colIndex = 0;
            foreach (ReportElement element in Elements) element.SQLColumnName = Source.IsNoSQL ? element.MetaColumn.Name : string.Format("C{0}", colIndex++);
        }

        void AddSubReportsElements()
        {
            //Add elements for sub-reports
            foreach (var el in Elements.Where(i => i.MetaColumn.SubReports.Count > 0 && i.PivotPosition != PivotPosition.Data).ToList())
            {
                foreach (var subreport in el.MetaColumn.SubReports)
                {
                    foreach (var guid in subreport.Restrictions)
                    {
                        if (!Elements.Exists(i => i.MetaColumnGUID == guid))
                        {
                            //Add the element
                            ReportElement element = ReportElement.Create();
                            element.Source = Source;
                            element.Model = this;
                            element.MetaColumnGUID = guid;
                            element.PivotPosition = PivotPosition.Hidden;
                            element.IsForNavigation = true;
                            element.SortOrder = SortOrderConverter.kNoSortKeyword;
                            Elements.Add(element);
                        }
                    }
                }
            }
        }

        DateTime _buildTimer;
        public void BuildSQL()
        {
            try
            {
                _sql = "";
                ExecutionError = "";

                if (Source.MetaData == null) return;

                AddSubReportsElements();

                InitReferences();

                execSelectClause = new StringBuilder();
                execFromClause = new StringBuilder();
                execWhereClause = new StringBuilder(Restriction);
                execGroupByClause = new StringBuilder();
                execOrderByNameClause = new StringBuilder();
                execHavingClause = new StringBuilder(AggregateRestriction);
                execOrderByClause = new StringBuilder();

                //build restriction
                RestrictionText = "";
                foreach (ReportRestriction restriction in ExecutionRestrictions)
                {
                    if (restriction.HasValue) Helper.AddValue(ref RestrictionText, "\r\n", restriction.DisplayText);
                    execWhereClause = execWhereClause.Replace("[" + restriction.GUID + "]", restriction.SQLText);
                }
                if (Report.CheckingExecution)
                {
                    if (execWhereClause.ToString().Trim().Length == 0) execWhereClause.Append("1=0");
                    else execWhereClause.Append(" AND (1=0)");
                }

                foreach (ReportRestriction restriction in ExecutionAggregateRestrictions)
                {
                    if (restriction.HasValue) Helper.AddValue(ref RestrictionText, "\r\n", restriction.DisplayText);
                    execHavingClause = execHavingClause.Replace("[" + restriction.GUID + "]", restriction.SQLText);
                }

                if (Elements.Count > 0)
                {
                    _fromTables = new List<MetaTable>();
                    List<MetaJoin> joins = new List<MetaJoin>();
                    List<string> selectColumns = new List<string>();
                    List<string> groupByColumns = new List<string>();
                    SetColumnsName();
                    foreach (ReportElement element in Elements)
                    {
                        string sqlColumn = element.SQLColumn + " AS " + element.SQLColumnName;
                        if (!selectColumns.Contains(sqlColumn))
                        {
                            Helper.AddValue(ref execSelectClause, ",\r\n", "  " + sqlColumn);
                            selectColumns.Add(sqlColumn);
                        }

                        MetaTable table = element.MetaColumn.MetaTable;
                        if (table != null && !_fromTables.Contains(table)) _fromTables.Add(table);

                        if (element.PivotPosition != PivotPosition.Data && !groupByColumns.Contains(element.SQLColumn))
                        {
                            Helper.AddValue(ref execGroupByClause, ",", element.SQLColumn);
                            groupByColumns.Add(element.SQLColumn);
                        }
                    }

                    foreach (ReportRestriction restriction in ExecutionRestrictions.Union(ExecutionAggregateRestrictions))
                    {
                        MetaTable table = restriction.MetaColumn.MetaTable;
                        if (table != null && !_fromTables.Contains(table) && restriction.HasValue && restriction.Operator != Operator.ValueOnly) _fromTables.Add(table);
                    }

                    if (GetElements(PivotPosition.Data).Count() == 0 && execHavingClause.Length == 0) execGroupByClause = new StringBuilder();

                    List<string> orderColumns = new List<string>();
                    UpdateFinalSortOrders();
                    buildOrderClause(GetElements(PivotPosition.Page), orderColumns, ref execOrderByClause, ref execOrderByNameClause);
                    buildOrderClause(GetElements(PivotPosition.Row), orderColumns, ref execOrderByClause, ref execOrderByNameClause);
                    buildOrderClause(GetElements(PivotPosition.Column), orderColumns, ref execOrderByClause, ref execOrderByNameClause);
                    buildOrderClause(GetElements(PivotPosition.Data), orderColumns, ref execOrderByClause, ref execOrderByNameClause);

                    List<MetaTable> extraWhereTables = _fromTables.Where(i => !string.IsNullOrEmpty(i.WhereSQL)).ToList();
                    if (_fromTables.Count == 1)
                    {
                        execFromClause = new StringBuilder(_fromTables[0].FullSQLName + "\r\n");
                    }
                    else
                    {
                        //multiple tables, find joins...
                        List<MetaTable> tablesToUse = _fromTables.ToList();
                        List<JoinPath> resultPaths = new List<JoinPath>();
                        JoinPath bestPath = null;
                        _buildTimer = DateTime.Now;
                        foreach (var leftTable in _fromTables)
                        {
                            JoinPath rootPath = new JoinPath() { currentTable = leftTable, joinsToUse = new List<MetaJoin>(Source.MetaData.Joins) };
                            rootPath.tablesToUse = new List<MetaTable>(_fromTables.Where(i => i.GUID != leftTable.GUID));
                            JoinTables(rootPath, resultPaths);

                            //Optimisation: if many result paths, check if we have aready a relevant result, here a number of join almost equal to number of tables to reach
                            if ((DateTime.Now - _buildTimer).TotalMilliseconds > BuildTimeout / 2)
                            {
                                bestPath = resultPaths.Where(i => i.tablesToUse.Count == 0).OrderByDescending(i => i.rank).ThenBy(i => i.joins.Count).FirstOrDefault();
                                if (bestPath != null && bestPath.joins.Count <= _fromTables.Count + 2)
                                {
                                    Debug.WriteLine("Exiting the joins search after xx seconds");
                                    break;
                                }
                            }
                            //Debug.WriteLine("{0}ms {1}", (DateTime.Now - _timer).TotalMilliseconds, resultPaths.Count);
                        }
#if DEBUG
                        foreach (var path in resultPaths.OrderByDescending(i => i.rank).ThenBy(i => i.tablesToUse.Count))
                        {
                            path.print();
                        }
#endif

                        //Choose the path having all tables, then preferred, then less joins...
                        if (bestPath == null) bestPath = resultPaths.Where(i => i.tablesToUse.Count == 0).OrderByDescending(i => i.rank).ThenBy(i => i.joins.Count).FirstOrDefault();
                        if (bestPath == null)
                        {
                            List<JoinPath> resultPaths2 = new List<JoinPath>();
                            //no direct joins found...try using several path...
                            foreach (var path in resultPaths.OrderByDescending(i => i.rank).ThenBy(i => i.tablesToUse.Count))
                            {
                                JoinPath newPath = new JoinPath() { joins = new List<MetaJoin>(path.joins), tablesToUse = new List<MetaTable>(path.tablesToUse), rank = path.rank };
                                foreach (var join in path.joins)
                                {
                                    //search a path starting from RightTable and finishing by a remaining table
                                    foreach (var path2 in resultPaths.OrderByDescending(i => i.rank).ThenBy(i => i.tablesToUse.Count).Where(i => ( (i.startTable == join.RightTable && path.tablesToUse.Contains(i.finalTable)) || (i.finalTable == join.RightTable && path.tablesToUse.Contains(i.startTable)))))
                                    {
                                        //ok add joins to the newPath and remove tables to use
                                        newPath.rank += path2.rank;
                                        int index = path.joins.IndexOf(join);
                                        foreach (var join2 in path2.joins)
                                        {
                                            //Note that we insert the joins just before the join having the RightTable...
                                            if (!newPath.joins.Exists(i => i.GUID == join2.GUID))
                                            {
                                                newPath.joins.Insert(index, join2);
                                            }
                                            newPath.tablesToUse.Remove(join2.LeftTable);
                                            newPath.tablesToUse.Remove(join2.RightTable);
                                        }

                                        if (newPath.tablesToUse.Count == 0)
                                        {
                                            //got one
                                            resultPaths2.Add(newPath);
                                            break;
                                        }
                                    }

                                    if (newPath.tablesToUse.Count == 0) break;
                                }

                                if ((DateTime.Now - _buildTimer).TotalMilliseconds > BuildTimeout / 2)
                                {
                                    bestPath = resultPaths2.Where(i => i.tablesToUse.Count == 0).OrderByDescending(i => i.rank).ThenBy(i => i.joins.Count).FirstOrDefault();
                                    if (bestPath != null)
                                    {
                                        Debug.WriteLine("Exiting the joins search after xx seconds");
                                        break;
                                    }
                                }
                            }
                            bestPath = resultPaths2.Where(i => i.tablesToUse.Count == 0).OrderByDescending(i => i.rank).ThenBy(i => i.joins.Count).FirstOrDefault();
                        }

                        if (bestPath == null) throw new Exception("Unable to link all elements using the joins defined...\r\nAdd Joins to your Data Source\r\nOR remove elements or restrictions in your model\r\nOR add relevant elements or restrictions in your model.");

                        if (bestPath.joins.Count == 0)
                        {
                            //only one table
                            execFromClause = new StringBuilder(bestPath.currentTable.FullSQLName + "\r\n");
                        }
                        else
                        {
                            /* this was the old flat method that does not work with Access...but produce a clean select 
                            foreach (MetaJoin join in bestPath.joins)
                            {
                                if (execFromClause.Length == 0) execFromClause = new StringBuilder(join.LeftTable.FullSQLName + "\r\n");
                                execFromClause.AppendFormat("{0} {1} ON {2}\r\n", join.SQLJoinType, join.RightTable.FullSQLName, join.Clause.Trim());
                            }*/
                            bestPath.print();
                            string lastTable = null;
                            List<MetaTable> tablesUsed = new List<MetaTable>();
                            for (int i = bestPath.joins.Count - 1; i >= 0; i--)
                            {
                                MetaJoin join = bestPath.joins[i];
                                if (string.IsNullOrEmpty(lastTable))
                                {
                                    lastTable = join.RightTable.FullSQLName + "\r\n";
                                    tablesUsed.Add(join.RightTable);
                                }

                                //check if tables are already in the join
                                var leftTable = join.LeftTable;
                                if (tablesUsed.Contains(leftTable)) leftTable = join.RightTable;
                                if (tablesUsed.Contains(leftTable)) continue;

                                string joinClause = join.Clause.Trim();
                                //For outer join, add the extra restriction in the ON clause -> hopefully they are not defined as bi-directional
                                MetaTable extraWhereTable = null;
                                if (join.JoinType == JoinType.LeftOuter && !string.IsNullOrEmpty(join.RightTable.WhereSQL)) extraWhereTable = join.RightTable;
                                else if (join.JoinType == JoinType.RightOuter && !string.IsNullOrEmpty(join.LeftTable.WhereSQL)) extraWhereTable = join.LeftTable;
                                else if (!string.IsNullOrEmpty(leftTable.WhereSQL) && !extraWhereTables.Contains(leftTable))
                                {
                                    extraWhereTables.Add(leftTable);
                                }

                                if (extraWhereTable != null)
                                {
                                    string where = Helper.ParseRazor(extraWhereTable.WhereSQL, extraWhereTable);
                                    if (!string.IsNullOrEmpty(where)) joinClause += " AND " + where;
                                    extraWhereTables.Remove(extraWhereTable);
                                }

                                //finally build the clause
                                if (join.JoinType != JoinType.Cross) lastTable = string.Format("\r\n({0} {1} {2} ON {3})\r\n", leftTable.FullSQLName, join.SQLJoinType, lastTable, joinClause);
                                else lastTable = string.Format("\r\n({0} {1} {2})\r\n", leftTable.FullSQLName, join.SQLJoinType, lastTable);

                                tablesUsed.Add(leftTable);
                            }
                            execFromClause = new StringBuilder(lastTable);
                        }
                    }

                    //add extra where clause
                    foreach (var table in extraWhereTables)
                    {
                        if (!string.IsNullOrEmpty(table.WhereSQL))
                        {
                            string where = Helper.ParseRazor(table.WhereSQL, table);
                            if (!string.IsNullOrEmpty(where))
                            {
                                if (execWhereClause.Length != 0) execWhereClause.Append("\r\nAND ");
                                execWhereClause.AppendFormat("({0})", where);
                            }
                        }
                    }

                    execSelect = execGroupByClause.Length > 0 ? "SELECT\r\n" : "SELECT DISTINCT\r\n";
                    execSelect = !string.IsNullOrEmpty(SqlSelect) ? SqlSelect : execSelect;
                    _sql = execSelect;
                    _sql += string.Format("{0}\r\n", execSelectClause);
                    _sql += !string.IsNullOrEmpty(SqlFrom) ? SqlFrom : string.Format("FROM {0}", execFromClause);
                    if (execWhereClause.Length > 0) _sql += string.Format("WHERE {0}\r\n", execWhereClause);
                    if (execGroupByClause.Length > 0) _sql += (!string.IsNullOrEmpty(SqlGroupBy) ? SqlGroupBy : string.Format("GROUP BY {0}", execGroupByClause)) + "\r\n";
                    if (execHavingClause.Length > 0) _sql += string.Format("HAVING {0}\r\n", execHavingClause);
                    if (execOrderByClause.Length > 0) _sql += (!string.IsNullOrEmpty(SqlOrderBy) ? SqlOrderBy : string.Format("ORDER BY {0}", execOrderByClause)) + "\r\n";
                }
            }
            catch (TemplateCompilationException ex)
            {
                _sql = "";
                ExecutionError = string.Format("Got unexpected error when building the SQL statement:\r\n{0}", Helper.GetExceptionMessage(ex));
            }
            catch (Exception ex)
            {
                _sql = "";
                ExecutionError = string.Format("Got unexpected error when building the SQL statement:\r\n{0}", ex.Message);
            }
        }

        class JoinPath
        {
            public MetaTable currentTable = null;
            public MetaTable startTable = null;
            public MetaTable finalTable = null;
            public List<MetaJoin> joins = new List<MetaJoin>();
            public List<MetaTable> tablesToUse;
            public List<MetaJoin> joinsToUse;
            public int rank = 0;

            public void print()
            {
#if DEBUG
                bool isFirst = true;
                foreach (var join in joins)
                {
                    if (isFirst) Debug.Write(join.LeftTable.DisplayName + "->");
                    Debug.Write(join.RightTable.DisplayName + "->");
                    isFirst = false;
                }
                Debug.WriteLine("");
                foreach (var join in joins)
                {
                    //    Debug.Write(string.Format("{0} {1} {2}\r\n", join.LeftTable.DisplayName, join.RightTable.DisplayName, join.Clause.Trim()));
                }
                Debug.WriteLine("");

#endif

            }
        }

        void JoinTables(JoinPath path, List<JoinPath> resultPath)
        {
            //TODO: optimize speed of this procedure...
            //If the search is longer than xx seconds, we exit with the first path found...
            if ((DateTime.Now - _buildTimer).TotalSeconds > BuildTimeout)
            {
                if (resultPath.Exists(i => i.tablesToUse.Count == 0))
                {
                    Debug.WriteLine("Exiting the joins search after build timeout");
                    return;
                }
            }

            if (path.tablesToUse.Count != 0)
            {
                foreach (var join in path.joinsToUse.Where(i => i.LeftTableGUID == path.currentTable.GUID || (i.RightTableGUID == path.currentTable.GUID && i.IsBiDirectional)))
                {
                    //Check that the new table has not already been reached
                    if (path.joins.Exists(i => i.RightTable == (join.RightTable == path.currentTable && join.IsBiDirectional ? join.LeftTable : join.RightTable))) continue;

                    MetaTable newTable = join.RightTable;
                    MetaJoin newJoin = join;
                    if (join.RightTable == path.currentTable && join.IsBiDirectional)
                    {
                        //Create a new join having the other left-right
                        newJoin = MetaJoin.Create();
                        newJoin.GUID = join.GUID;
                        newJoin.Source = join.Source;
                        newJoin.LeftTableGUID = join.RightTableGUID;
                        newJoin.RightTableGUID = join.LeftTableGUID;
                        newJoin.JoinType = join.JoinType;
                        newJoin.Clause = join.Clause;
                        //In this case the next table is the left one...
                        newTable = join.LeftTable;
                    }
                    //if (_level == 1) Debug.WriteLine("{0} {1}", resultPath.Count, newTable.Name);

                    JoinPath newJoinPath = new JoinPath() { joins = new List<MetaJoin>(path.joins), tablesToUse = new List<MetaTable>(path.tablesToUse), joinsToUse = new List<MetaJoin>(path.joinsToUse), rank = path.rank };
                    //add the join and continue the path
                    newJoinPath.currentTable = newTable;
                    newJoinPath.joins.Add(newJoin);
                    newJoinPath.tablesToUse.Remove(newTable);
                    newJoinPath.joinsToUse.Remove(join);
                    //Set preferred path
                    if (newTable == ForceJoinTable) newJoinPath.rank++;
                    if (newTable == AvoidJoinTable) newJoinPath.rank--;
                    JoinTables(newJoinPath, resultPath);
                }
            }
            if (path.joins.Count > 0 && _fromTables.Contains(path.joins.Last().RightTable))
            {
                path.startTable = path.joins.First().LeftTable;
                path.finalTable = path.joins.Last().RightTable;
                resultPath.Add(path);
            }
        }

        public void UpdateFinalSortOrders()
        {
            updateFinalSortOrder(GetElements(PivotPosition.Page));
            updateFinalSortOrder(GetElements(PivotPosition.Row));
            updateFinalSortOrder(GetElements(PivotPosition.Column));
            updateFinalSortOrder(GetElements(PivotPosition.Data));
        }

        void updateFinalSortOrder(IEnumerable<ReportElement> elements)
        {
            for (int i = 0; i < elements.Count(); i++)
            {
                ReportElement element = elements.ElementAt(i);
                if (element.SortOrder != SortOrderConverter.kNoSortKeyword)
                {
                    if (element.SortOrder == SortOrderConverter.kAutomaticAscSortKeyword) element.FinalSortOrder = string.Format("{0} {1}", i, SortOrderConverter.kAscendantSortKeyword);
                    else if (element.SortOrder == SortOrderConverter.kAutomaticDescSortKeyword) element.FinalSortOrder = string.Format("{0} {1}", i, SortOrderConverter.kDescendantSortKeyword);
                    else element.FinalSortOrder = element.SortOrder;
                }
            }
        }

        void buildOrderClause(IEnumerable<ReportElement> elements, List<string> orderColumns, ref StringBuilder orderClause, ref StringBuilder orderClauseName)
        {
            foreach (ReportElement element in elements.OrderBy(i => i.FinalSortOrder))
            {
                if (!orderColumns.Contains(element.SQLColumn) && element.IsSorted)
                {
                    string ascdesc = element.SortOrder.Contains(SortOrderConverter.kAscendantSortKeyword) ? " ASC" : " DESC";
                    Helper.AddValue(ref orderClause, ",", (Source.IsNoSQL ? element.SQLColumnName : element.SQLColumn) + ascdesc); //If NoSQL, the Data View can not be sorted with aggregate...
                    Helper.AddValue(ref orderClauseName, ",", element.SQLColumnName + ascdesc);
                    orderColumns.Add(element.SQLColumn);
                }
            }
        }

        DbCommand _command;
        public void CancelCommand()
        {
            if (_commandMutex.WaitOne(1000))
            {
                try
                {
                    if (_command != null)
                    {
                        _command.Cancel();
                    }
                }
                finally
                {
                    _commandMutex.ReleaseMutex();
                }
            }
        }
        Mutex _commandMutex = new Mutex();

        void executePrePostStatement(string sql, string prefix, string name, bool ignoreErrors, object model)
        {
            if (!string.IsNullOrEmpty(sql))
            {
                try
                {
                    Report.LogMessage("Model '{0}': Executing {1}-SQL statement for '{2}'...", Name, prefix, name);
                    string finalSql = Helper.ParseRazor(sql, model);
                    if (!string.IsNullOrEmpty(finalSql))
                    {
                        _command.CommandText = finalSql;
                        _command.ExecuteNonQuery();
                    }
                    else Report.LogMessage("No SQL to execute...");
                }
                catch (Exception ex)
                {
                    string error = string.Format("Model '{0}': Error got during execution of {1}-SQL statement for '{2}'\r\n{3}", Name, prefix, name, ex.Message);
                    if (ignoreErrors) Report.LogMessage(error);
                    else throw new Exception(error);
                }
            }
        }
        void executePrePostStatements(bool isPre)
        {
            foreach (var table in _fromTables)
            {
                executePrePostStatement(isPre ? table.PreSQL : table.PostSQL, isPre ? "Pre" : "Post", table.Name, table.IgnorePrePostError, table);
            }
        }

        public void ExecuteLoadScript(string script, string name, object model)
        {
            if (!string.IsNullOrEmpty(script))
            {
                try
                {
                    Helper.ParseRazor(script, model);
                }
                catch (Exception razorException)
                {
                    throw new Exception(string.Format("Error when executing '{0}'.\r\n{1}", name, razorException.Message));
                }
            }
        }


        public static Dictionary<string, ReportModel> RunningModels = new Dictionary<string, ReportModel>();
        void checkRunningModels(string key)
        {
            lock (RunningModels)
            {
                if (RunningModels.ContainsKey(key))
                {
                    //check if we can reuse the current running query: same source, same connection string and same pre/Post SQL
                    ReportModel runningModel = RunningModels[key];
                    if (Source == runningModel.Source
                        && Connection.FullConnectionString == runningModel.Connection.FullConnectionString
                        && string.IsNullOrEmpty(runningModel.ExecutionError)
                        && ((PreSQL == null && runningModel.PreSQL == null) || (PreSQL.ToLower().Trim() == runningModel.PreSQL.ToLower().Trim()))
                        && ((PostSQL == null && runningModel.PostSQL == null) || (PostSQL.ToLower().Trim() == runningModel.PostSQL.ToLower().Trim()))
                        && ((PreLoadScript == null && runningModel.PreLoadScript == null) || (PreLoadScript.ToLower().Trim() == runningModel.PreLoadScript.ToLower().Trim()))
                        )
                    {
                        //we can wait to get the same data table 
                        Report.LogMessage("Model '{0}': Getting result table from '{1}'...", Name, runningModel.Name);
                        while (!Report.Cancel && !runningModel._resultTableAvailable)
                        {
                            if (!string.IsNullOrEmpty(runningModel.ExecutionError)) break;
                            Thread.Sleep(100);
                        }

                        if (runningModel.ResultTable != null)
                        {
                            //Set the result table
                            ResultTable = Source.IsNoSQL ? runningModel._rawNoSQLTable : runningModel.ResultTable;
                            ExecutionDuration = runningModel.ExecutionDuration;
                        }
                    }
                }
                else
                {
                    RunningModels.Add(key, this);
                }
            }
        }

        [XmlIgnore]
        private DataTable _rawNoSQLTable = null;
        [XmlIgnore]
        private bool _resultTableAvailable = false;

        public void FillResultTable()
        {
            ExecutionDuration = 0;
            _resultTableAvailable = false;
            Pages.Clear();

            //Pre-load script
            if (!Source.IsNoSQL) ExecuteLoadScript(PreLoadScript, "Pre Load Script", this);

            ExecutionError = "";

            BuildSQL();

            ResultTable = null;
            _command = null;

            if (Source.IsNoSQL && Elements.Count > 0 && !Report.Cancel)
            {
                //No SQL
                try
                {
                    Source.MetaData.MasterTable.Log = Report;
                    string key = !string.IsNullOrEmpty(LoadScript) ? LoadScript : "_Master_" + Source.MetaData.MasterTable.LoadScript;

                    //Check if we can use a data table from another model
                    checkRunningModels(key);

                    if (ResultTable == null)
                    {
                        if (!string.IsNullOrEmpty(LoadScript))
                        {
                            ResultTable = Source.MetaData.MasterTable.BuildNoSQLTable(false);
                            Helper.ParseRazor(LoadScript, this);
                        }
                        else
                        {
                            ResultTable = Source.MetaData.MasterTable.BuildNoSQLTable(true);
                        }
                        _rawNoSQLTable = ResultTable;
                    }

                    //apply filters using DataView
                    if (execWhereClause.Length > 0 || execOrderByClause.Length > 0)
                    {
                        //make a copy first
                        ResultTable = ResultTable.Copy();
                        var dataView = new DataView(ResultTable);
                        dataView.RowFilter = execWhereClause.ToString();
                        dataView.Sort = execOrderByClause.ToString();
                        ResultTable = dataView.ToTable();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Got unexpected error when building NoSQL Table:\r\n{0}\r\n", ex.Message));
                }
            }
            else if (!string.IsNullOrEmpty(_sql) && !Report.Cancel)
            {
                //Normal SQL
                try
                {
                    if (string.IsNullOrEmpty(Connection.ConnectionString)) throw new Exception("The connection string is not defined for this Model.");
                    _command = null;

                    //Check if we can use a data table from another model
                    checkRunningModels(_sql);

                    if (ResultTable == null)
                    {
                        DbConnection connection = null;
                        if (_commandMutex.WaitOne(1000))
                        {
                            try
                            {
                                connection = Connection.GetOpenConnection();
                                if (connection is OdbcConnection) _command = ((OdbcConnection)connection).CreateCommand();
                                else _command = ((OleDbConnection)connection).CreateCommand();
                                _command.CommandTimeout = 0;
                                ExecutionDate = DateTime.Now;
                            }
                            finally
                            {
                                _commandMutex.ReleaseMutex();
                            }
                        }
                        else
                        {
                            throw new Exception("Unable to get command mutex...");
                        }
                        executePrePostStatement(Source.PreSQL, "Pre", Source.Name, Source.IgnorePrePostError, Source);
                        executePrePostStatements(true);
                        executePrePostStatement(PreSQL, "Pre", Name, IgnorePrePostError, this);
                        Report.LogMessage("Model '{0}': Executing main query...", Name);
                        _command.CommandText = _sql;

                        ResultTable = new DataTable();
                        DbDataAdapter adapter = null;
                        if (connection is OdbcConnection) adapter = new OdbcDataAdapter((OdbcCommand)_command);
                        else adapter = new OleDbDataAdapter((OleDbCommand)_command);
                        adapter.Fill(ResultTable);
                        executePrePostStatement(PostSQL, "Post", Name, IgnorePrePostError, this);
                        executePrePostStatements(false);
                        executePrePostStatement(Source.PostSQL, "Post", Source.Name, Source.IgnorePrePostError, Source);
                        connection.Close();
                        _command = null;
                        ExecutionDuration = Convert.ToInt32((DateTime.Now - ExecutionDate).TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Unexpected error when executing the following SQL statement:\r\n{0}\r\n\r\nError detail:\r\n{1}", _sql, ex.Message));
                }

                if (Report.Cancel) return;

                //If enum, set enum values directly in the table
                bool hasEnum = false;
                List<ReportElement> specialSortByPositionElements = new List<ReportElement>();
                foreach (var element in Elements)
                {
                    if (element.IsEnum)
                    {
                        lock (ResultTable)
                        {
                            hasEnum = true;
                            DataColumn col = ResultTable.Columns[element.SQLColumnName];
                            DataColumn newcol = new DataColumn("_seal_dummy_temp_col_", typeof(string));
                            ResultTable.Columns.Add(newcol);

                            foreach (DataRow row in ResultTable.Rows)
                            {
                                //to sort by position, we add 6 digits as a prefix
                                if (element.IsSorted && element.EnumEL.UsePosition && !specialSortByPositionElements.Contains(element)) specialSortByPositionElements.Add(element);
                                row[newcol] = element.GetEnumSortValue(row[col].ToString(), false);
                            }
                            ResultTable.Columns.Remove(col);
                            newcol.ColumnName = element.SQLColumnName;
                        }
                    }
                }

                if (Report.Cancel) return;

                if (hasEnum)
                {
                    //this re-sort the result with enum values...
                    DataView dv = new DataView(ResultTable, null, execOrderByNameClause.ToString(), DataViewRowState.CurrentRows);
                    ResultTable = dv.ToTable();

                    if (Report.Cancel) return;

                    //remove the 6 digits used for special sort
                    foreach (var element in specialSortByPositionElements)
                    {
                        DataColumn col = ResultTable.Columns[element.SQLColumnName];
                        foreach (DataRow row in ResultTable.Rows)
                        {
                            string newValue = row[col].ToString();
                            if (newValue.Length > 5) row[col] = newValue.Substring(6);
                        }
                    }
                }

                ExecuteLoadScript(LoadScript, "Post Load Script", this);
            }
            _resultTableAvailable = true;
        }


        public void InvertDataTables()
        {
            foreach (ResultPage page in Pages)
            {
                if (Report.Cancel) break;

                if (page.DataTable.Lines.Count > 0 && !page.DataTable.InvertDone)
                {
                    ResultTable newTable = new ResultTable();
                    for (int i = 0; i < page.DataTable.Lines[0].Length; i++)
                    {
                        ResultCell[] newLine = new ResultCell[page.DataTable.Lines.Count];
                        for (int j = 0; j < page.DataTable.Lines.Count; j++)
                        {
                            newLine[j] = page.DataTable.Lines[j][i];
                        }
                        newTable.Lines.Add(newLine);
                    }

                    //Revert start row and column
                    newTable.BodyStartRow = page.DataTable.BodyStartColumn;
                    newTable.BodyStartColumn = page.DataTable.BodyStartRow;
                    //Calculate body end
                    for (int i = 0; i < page.DataTable.Lines[0].Length; i++)
                    {
                        if (!page.DataTable.Lines[0][i].IsTotal) newTable.BodyEndRow++;
                    }
                    page.DataTable = newTable;
                    page.DataTable.InvertDone = true;
                }
            }
        }

        public string CheckSQL(string sql, List<MetaTable> tables, ReportModel model)
        {
            string result = "";
            if (!string.IsNullOrEmpty(sql))
            {
                try
                {
                    DbConnection connection = Connection.GetOpenConnection();
                    Helper.ExecutePrePostSQL(connection, PreSQL, this, this.IgnorePrePostError);
                    if (tables != null) foreach (var table in tables) Helper.ExecutePrePostSQL(connection, table.PreSQL, table, table.IgnorePrePostError);
                    if (model != null) Helper.ExecutePrePostSQL(connection, model.PreSQL, model, model.IgnorePrePostError);
                    var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.ExecuteReader();
                    if (model != null) Helper.ExecutePrePostSQL(connection, model.PostSQL, model, model.IgnorePrePostError);
                    if (tables != null) foreach (var table in tables) Helper.ExecutePrePostSQL(connection, table.PostSQL, table, table.IgnorePrePostError);
                    Helper.ExecutePrePostSQL(connection, PostSQL, this, this.IgnorePrePostError);
                    command.Connection.Close();
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
            }

            return result;
        }

        public ReportRestriction GetRestrictionByName(string name)
        {
            return Restrictions.FirstOrDefault(i => i.DisplayNameEl.ToLower() == name.ToLower());
        }


        public string GetNavigation(ResultCell cell, bool serverSide = false)
        {
            string navigation = "";
            if (Report.GenerateHTMLDisplay || serverSide)
            {
                foreach (var link in cell.Links)
                {
                    if ((link.Href.StartsWith("exe=") && Report.View.GetBoolValue("drill_enabled")) || (link.Href.StartsWith("rpa=") && Report.View.GetBoolValue("sub_report_enabled")))
                    {
                        navigation += string.Format("<li nav='{0}'><a href='#'>{1}</a></li>", link.Href, link.Text);
                    }
                }
                navigation = string.IsNullOrEmpty(navigation) ? "" : (serverSide ? navigation : string.Format(" navigation=\"{0}\"", navigation));
            }
            return navigation;
        }

        //SANDBOX !
        //Just use this to code, compile and debug your Razor Script using Visual Studio...
        //When OK, just cut and paste it into the Load Script of your Model using the Report Designer
        public void DesignLoadScript()
        {
            var model = this;
            DataTable table = model.ResultTable;

            table.Columns.Clear();
            //Just replace model.DesignLoadScript(); with the code below
            table.Columns.Add(new DataColumn("Name", typeof(string)));
            table.Columns.Add(new DataColumn("RootDirectory", typeof(string)));
            table.Columns.Add(new DataColumn("AvailableFreeSpace", typeof(int)));
            table.Columns.Add(new DataColumn("DriveType", typeof(string)));
            table.Columns.Add(new DataColumn("TotalFreeSpace", typeof(int)));
            table.Columns.Add(new DataColumn("TotalSize", typeof(int)));
            table.Columns.Add(new DataColumn("VolumeLabel", typeof(string)));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                table.Rows.Add(drive.Name, drive.RootDirectory.Name, drive.AvailableFreeSpace / (1024 * 1024), drive.DriveType.ToString(), drive.TotalFreeSpace / (1024 * 1024), drive.TotalSize / (1024 * 1024), drive.VolumeLabel);
            }

            table.Columns.Clear();
            table.Columns.Add(new DataColumn("travel_mode", typeof(string)));
            table.Columns.Add(new DataColumn("duration", typeof(int)));
            table.Columns.Add(new DataColumn("duration_text", typeof(string)));
            table.Columns.Add(new DataColumn("html_instructions", typeof(string)));
            table.Columns.Add(new DataColumn("distance", typeof(int)));
            table.Columns.Add(new DataColumn("distance_text", typeof(string)));


            string url = "https://maps.googleapis.com/maps/api/directions/xml?origin=Toronto&destination=Montreal";

            var webRequest = System.Net.WebRequest.Create(url);
            var responseStream = webRequest.GetResponse().GetResponseStream();
            var reader = new StreamReader(responseStream);
            var xmlResponse = System.Xml.Linq.XElement.Parse(reader.ReadToEnd());
            var status = xmlResponse.Element("status").Value;
            if (status.ToLower() == "ok")
            {
                foreach (var step in xmlResponse.Element("route").Elements("leg").First().Elements("step"))
                {
                    var duration = int.Parse(step.Element("duration").Element("value").Value);
                    var distance = int.Parse(step.Element("distance").Element("value").Value);
                    table.Rows.Add(step.Element("travel_mode").Value, duration, step.Element("duration").Element("text").Value, step.Element("travel_mode").Value, distance, step.Element("distance").Element("text").Value);
                }
            }
        }
    }
}