using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Tharga.Toolkit.Console.Command.Base;

using SaintCoinach.Ex;
using SaintCoinach.Xiv;

#pragma warning disable CS1998

namespace SaintCoinach.Cmd.Commands
{
    public class JsonCommand : ActionCommandBase
    {
        private ARealmReversed _Realm;

        /// <summary>
        /// Setup the command
        /// </summary>
        /// <param name="realm"></param>
        public JsonCommand(ARealmReversed realm)
            : base("json", "Export all data (default), or only specific data files, including all languages. Line breaks are converted to HTML breaks.")
        {
            _Realm = realm;
        }

        /// <summary>
        /// Obtain game sheets from the game data
        /// </summary>
        /// <param name="paramList"></param>
        /// <returns></returns>
        public override async Task<bool> InvokeAsync(string paramList)
        {
            var versionPath = _Realm.GameVersion;
            if (paramList?.Contains("/UseDefinitionVersion") ?? false)
                versionPath = _Realm.DefinitionVersion;

            AssignVariables(this, paramList);

            const string JsonFileFormat = "json/{0}{1}.json";

            IEnumerable<string> filesToExport;

            // Gather files to export, may be split by params.
            if (string.IsNullOrWhiteSpace(paramList))
                filesToExport = _Realm.GameData.AvailableSheets;
            else
                filesToExport = paramList.Split(' ').Select(_ => _Realm.GameData.FixName(_));

            // Action counts
            var successCount = 0;
            var failCount = 0;
            var currentCount = 0;
            var total = filesToExport.Count();

            // Process game files.
            foreach (var name in filesToExport)
            {
                currentCount++;
                //if (name.Contains("/")) continue; // I don't care about these, save some time by skipping
                var sheet = _Realm.GameData.GetSheet(name);

                // Loop through all available languages
                foreach (var lang in sheet.Header.AvailableLanguages)
                {
                    var code = lang.GetCode();
                    if (code == "chs") continue; // no data for these languages anyway
                    if (code == "ko") continue;
                    if (code.Length > 0)
                        code = "." + code;

                    var target = new FileInfo(Path.Combine(versionPath, string.Format(JsonFileFormat, name, code)));

                    try
                    {
                        if (!target.Directory.Exists)
                            target.Directory.Create();

                        // Save
                        OutputInformation("[{0}/{1}] Processing: {2} - Language: {3}", currentCount, total, name, lang.GetSuffix());
                        //ExdHelper.SaveAsCsv(sheet, lang, target.FullName, true);
                        SaveAsJson(sheet, lang, target.FullName);
                        ++successCount;
                    }
                    catch (Exception e)
                    {
                        OutputError("Export of {0} failed: {1}", name, e.Message);
                        try { if (target.Exists) { target.Delete(); } } catch { }
                        ++failCount;
                    }
                }
            }
            OutputInformation("{0} files exported, {1} failed", successCount, failCount);

            return true;
        }

        void SaveAsJson(SaintCoinach.Ex.Relational.IRelationalSheet sheet, Language language, string path) {
            using (var s = new StreamWriter(path, false, Encoding.UTF8)) {
                var colIndices = new List<int>();
                var colNames = new List<string>();
                foreach (var col in sheet.Header.Columns) {
                    colIndices.Add(col.Index);
                    colNames.Add(col.Name);
                }
                s.WriteLine("[");
                WriteRows(s, sheet, language, colIndices, colNames);
                s.WriteLine();
                s.WriteLine("]");

            }
        }

        public void WriteRows(StreamWriter s, ISheet sheet, Language language, IEnumerable<int> colIndices, List<string> colNames) {
            if (sheet.Header.Variant == 1)
                WriteRowsCore(s, sheet.Cast<Ex.IRow>(), language, colIndices, colNames, WriteRowKey);
            else {
                var rows = sheet.Cast<Xiv.XivRow>().Select(_ => (Ex.Variant2.DataRow)_.SourceRow);
                foreach (var parentRow in rows.OrderBy(_ => _.Key))
                    WriteRowsCore(s, parentRow.SubRows, language, colIndices, colNames, WriteSubRowKey);
            }
        }

        void WriteRowsCore(StreamWriter s, IEnumerable<Ex.IRow> rows, Language language, IEnumerable<int> colIndices, List<string> colNames, Action<StreamWriter, Ex.IRow> writeKey) {
            bool writeRaw = true;
            foreach (var row in rows.OrderBy(_ => _.Key)) {
                var useRow = row;

                if (useRow is IXivRow)
                    useRow = ((IXivRow)row).SourceRow;
                var multiRow = useRow as IMultiRow;
                
                if (useRow.Key > 0) s.WriteLine(",");

                s.WriteLine("{");
                s.Write("  \"Id\":");
                writeKey(s, useRow);
                foreach (var col in colIndices) {
                    object v;
                    if (language == Language.None || multiRow == null)
                        v = writeRaw ? useRow.GetRaw(col) : useRow[col];
                    else
                        v = writeRaw ? multiRow.GetRaw(col, language) : multiRow[col, language];


                    s.WriteLine(",");
                    s.Write("  ");

                    if (colNames[col] is string)
                        s.Write("\"" + colNames[col] + "\":");
                    else
                        s.Write("\"" + col + "\":");

                    if (v == null)
                        s.Write("");
                    else if (v is Boolean && (bool)v == true)
                        s.Write("true");
                    else if (v is Boolean && (bool)v == false)
                        s.Write("false");
                    else if (v is IDictionary<int, object>)
                        WriteDict(s, v as IDictionary<int, object>);
                    else if (IsUnescaped(v))
                        s.Write("{0}", v);
                    else
                        s.Write("\"{0}\"", v.ToString().Replace("\r\n", "<br />").Replace("\n", "<br />").Replace("\r", "<br />").Replace("\"", "\\\""));
                    
                }
                s.WriteLine();
                s.Write("}");

                s.Flush();
            }
        }

        void WriteRowKey(StreamWriter s, Ex.IRow row) {
            s.Write(row.Key);
        }

        void WriteSubRowKey(StreamWriter s, Ex.IRow row) {
            var subRow = (Ex.Variant2.SubRow)row;
            s.Write(subRow.FullKey);
        }

        void WriteDict(StreamWriter s, IDictionary<int, object> v) {
            s.Write("\"");
            var isFirst = true;
            foreach (var kvp in v) {
                if (isFirst)
                    isFirst = false;
                else
                    s.Write(",");
                s.Write("[{0},", kvp.Key);
                if (kvp.Value != null)
                    s.Write(kvp.Value.ToString().Replace("\"", "\\\""));
                s.Write("]");
            }
            s.Write("\"");
        }

        bool IsUnescaped(object self) {
            return (self is Boolean
                || self is Byte
                || self is SByte
                || self is Int16
                || self is Int32
                || self is Int64
                || self is UInt16
                || self is UInt32
                || self is UInt64
                || self is Single
                || self is Double);
        }
    }
}