﻿using Implem.Libraries.Classes;
using Implem.Libraries.DataSources.SqlServer;
using Implem.Libraries.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
namespace Implem.DefinitionAccessor
{
    public class Initializer
    {
        public static void Initialize(
            string path, bool codeDefiner = false,
            bool setSaPassword = false,
            bool setRandomPassword = false)
        {
            Environments.CodeDefiner = codeDefiner;
            Environments.CurrentDirectoryPath = GetCurrentDirectoryPath(
                new FileInfo(path).Directory);
            SetRdsPassword(setSaPassword, setRandomPassword);
            SetParameters();
            Environments.ServiceName = Parameters.Service.Name;
            SetRdsParameters();
            Environments.MachineName = Environment.MachineName;
            Environments.Application = 
                Assembly.GetExecutingAssembly().ManifestModule.Name.FileNameOnly();
            Environments.AssemblyVersion = 
                Assembly.GetExecutingAssembly().GetName().Version.ToString();
            SetDefinitions();
            Environments.TimeZoneInfoDefault = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(o => o.Id == Parameters.Service.TimeZoneDefault);
            SetSqls();
            DateTimes.FirstDayOfWeek = Parameters.General.FirstDayOfWeek;
            DateTimes.FirstMonth = Parameters.General.FirstMonth;
        }

        private static void SetRdsPassword(bool setRdsPassword, bool setRandomPassword)
        {
            if (setRdsPassword)
            {
                Console.WriteLine("Please enter the SA password.");
                var rdsParameters = Files.Read(ParametersPath("Rds"));
                rdsParameters = Regex.Replace(
                    rdsParameters,
                    "(?<=UID\\=sa;PWD\\=).*?(?=;)",
                    Console.ReadLine());
                if (setRandomPassword)
                {
                    rdsParameters = Regex.Replace(
                        rdsParameters,
                        "(?<=UID\\=#ServiceName#_Owner;PWD\\=).*?(?=;)",
                        Strings.NewGuid());
                    rdsParameters = Regex.Replace(
                        rdsParameters,
                        "(?<=UID\\=#ServiceName#_User;PWD\\=).*?(?=;)",
                        Strings.NewGuid());
                }
                rdsParameters.Write(ParametersPath("Rds"));
            }
        }

        private static void SetParameters()
        {
            Parameters.Authentication = Files.Read(ParametersPath("Authentication"))
                .Deserialize<ParameterAccessor.Parts.Authentication>();
            Parameters.BinaryStorage = Files.Read(ParametersPath("BinaryStorage"))
                .Deserialize<ParameterAccessor.Parts.BinaryStorage>();
            Parameters.General = Files.Read(ParametersPath("General"))
                .Deserialize<ParameterAccessor.Parts.General>();
            Parameters.Mail = Files.Read(ParametersPath("Mail"))
                .Deserialize<ParameterAccessor.Parts.Mail>();
            Parameters.Path = Files.Read(ParametersPath("Path"))
                .Deserialize<ParameterAccessor.Parts.Path>();
            Parameters.Rds = Files.Read(ParametersPath("Rds"))
                .Deserialize<ParameterAccessor.Parts.Rds>();
            Parameters.Service = Files.Read(ParametersPath("Service"))
                .Deserialize<ParameterAccessor.Parts.Service>();
            Parameters.SysLog = Files.Read(ParametersPath("SysLog"))
                .Deserialize<ParameterAccessor.Parts.SysLog>();
        }

        private static string ParametersPath(string name)
        {
            return Path.Combine(
                Environments.CurrentDirectoryPath,
                "App_Data",
                "Parameters",
                name + ".json");
        }

        private static string GetCurrentDirectoryPath(DirectoryInfo currentDirectory)
        {
            foreach (var sub in currentDirectory.GetDirectories())
            {
                var path = Path.Combine(
                    sub.FullName, "App_Data", "Parameters", "Service.json");
                if (Files.Exists(path))
                {
                    return new FileInfo(path).Directory.Parent.Parent.FullName;
                }
            }
            return GetCurrentDirectoryPath(currentDirectory.Parent);
        }

        public static void SetDefinitions()
        {
            Def.SetCodeDefinition();
            Def.SetColumnDefinition();
            Def.SetCssDefinition();
            Def.SetDataViewDefinition();
            Def.SetDemoDefinition();
            Def.SetJavaScriptDefinition();
            Def.SetDisplayDefinition();
            Def.SetSqlDefinition();
            SetDisplayDefinitionAdditional();
        }

        public static XlsIo DefinitionFile(string fileName)
        {
            var tempFile = new FileInfo(Files.CopyToTemp(
                Directories.Definitions(fileName), Directories.Temp()));
            var xlsIo = new XlsIo(tempFile.FullName);
            tempFile.Delete();
            if (fileName == "definition_Column.xlsm")
            {
                SetColumnDefinitionAdditional(xlsIo);
            }
            return xlsIo;
        }

        private static void SetRdsParameters()
        {
            Parameters.Rds.SaConnectionString = 
                Parameters.Rds.SaConnectionString.Replace(
                    "#ServiceName#", Environments.ServiceName);
            Parameters.Rds.OwnerConnectionString = 
                Parameters.Rds.OwnerConnectionString.Replace(
                    "#ServiceName#", Environments.ServiceName);
            Parameters.Rds.UserConnectionString = 
                Parameters.Rds.UserConnectionString.Replace(
                    "#ServiceName#", Environments.ServiceName);
            switch (Parameters.Rds.Provider)
            {
                case "Azure":
                    Environments.RdsProvider = "Azure";
                    Azures.SetRetryManager(
                        Parameters.Rds.SqlAzureRetryCount,
                        Parameters.Rds.SqlAzureRetryInterval);
                    break;
                default:
                    Environments.RdsProvider = "Local";
                    break;
            }
            Environments.RdsTimeZoneInfo = TimeZoneInfo.GetSystemTimeZones()
                .FirstOrDefault(o => o.Id == Parameters.Rds.TimeZoneInfo);
        }

        private static void SetColumnDefinitionAdditional(XlsIo definitionFile)
        {
            var tableCopy = definitionFile.XlsSheet.ToList<XlsRow>();
            var sheet = definitionFile.XlsSheet;
            definitionFile.XlsSheet.Select(o => new
            {
                ModelName = o["ModelName"].ToStr(),
                TableName = o["TableName"].ToStr(),
                Label = o["Label"].ToStr(),
                Base = o["Base"].ToBool()
            })
                .Where(o => !o.Base && o.TableName != "string")
                .Distinct()
                .ForEach(column =>
                    sheet.Where(o => o["Base"].ToBool()).ForEach(commonColumnDefinition =>
                    {
                        if (IsTargetColumn(sheet, commonColumnDefinition, column.TableName) && 
                            IsNotExists(tableCopy, commonColumnDefinition, column.TableName))
                        {
                            var copyColumnDefinition = new XlsRow();
                            definitionFile.XlsSheet.Columns.ForEach(xcolumn =>
                                copyColumnDefinition[xcolumn] = commonColumnDefinition[xcolumn]);
                            copyColumnDefinition["Id"] =
                                column.TableName + "_" + copyColumnDefinition["ColumnName"];
                            copyColumnDefinition["ModelName"] = column.ModelName;
                            copyColumnDefinition["TableName"] = column.TableName;
                            copyColumnDefinition["Label"] = column.Label;
                            copyColumnDefinition["Base"] = "0";
                            copyColumnDefinition["ItemId"] = "0";
                            tableCopy.Add(copyColumnDefinition);
                        }
                    }));
            definitionFile.XlsSheet = new XlsSheet(tableCopy, definitionFile.XlsSheet.Columns);
        }

        private static bool IsTargetColumn(
            XlsSheet sheet, XlsRow commonColumnDefinition, string tableName)
        {
            return commonColumnDefinition["ItemId"].ToInt() == 0 ||
                sheet.Any(o => o["TableName"].ToString() == tableName &&
                    o["ItemId"].ToInt() > 0);
        }

        private static bool IsNotExists(
            List<XlsRow> tableCopy, XlsRow commonColumnDefinition, string tableName)
        {
            return !tableCopy.Any(o => o["TableName"].ToString() == tableName &&
                o["ColumnName"].ToString() == commonColumnDefinition["ColumnName"].ToString());
        }

        private static void SetDisplayDefinitionAdditional()
        {
            Def.ColumnDefinitionCollection
                .Where(o => !o.Base)
                .Select(o => new { Id = o.Id, Content = o.ColumnLabel })
                .Union(Def.ColumnDefinitionCollection
                .Where(o => !o.Base)
                .Select(o => new { Id = o.TableName, Content = o.Label })
                .Distinct())
                .Where(o => !Def.DisplayDefinitionCollection.Any(p => p.Id == o.Id))
                .ForEach(data => Def.DisplayDefinitionCollection.Add(new DisplayDefinition()
                {
                    Id = data.Id,
                    Content = data.Content
                }));
        }

        private static void SetSqls()
        {
            Sqls.LogsPath = Directories.Logs();
            Sqls.BeginTransaction = Def.Sql.BeginTransaction;
            Sqls.CommitTransaction = Def.Sql.CommitTransaction;
        }
    }
}
