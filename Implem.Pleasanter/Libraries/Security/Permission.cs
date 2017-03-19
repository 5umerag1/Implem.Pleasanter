﻿using Implem.DefinitionAccessor;
using Implem.Libraries.Utilities;
using Implem.Pleasanter.Libraries.Responses;
using Implem.Pleasanter.Libraries.Server;
using Implem.Pleasanter.Libraries.Settings;
using Implem.Pleasanter.Models;
using System.Linq;
namespace Implem.Pleasanter.Libraries.Security
{
    public class Permission
    {
        SiteSettings SiteSettings;
        public string Name;
        public int Id;
        public Permissions.Types Type;
        public bool Source;

        public Permission(
            SiteSettings ss, string name, int id, Permissions.Types type, bool source = false)
        {
            SiteSettings = ss;
            Id = id;
            Name = name;
            Type = type;
            Source = source;
        }

        public string NameAndId()
        {
            return Name + "," + Id;
        }

        public string Key()
        {
            return Name + "," + Id + "," + Type.ToInt().ToString();
        }

        public ControlData ControlData(bool withType = true)
        {
            switch (Name)
            {
                case "Dept":
                    return DisplayText(
                        Displays.Depts(),
                        Id != 0
                            ? SiteInfo.Dept(Id)?.Name
                            : null,
                        withType);
                case "Group":
                    var groupModel = Id != 0
                        ? new GroupModel(SiteSettingsUtilities.GroupsSiteSettings(), Id)
                        : null;
                    return DisplayText(
                        Displays.Groups(),
                        groupModel?.AccessStatus == Databases.AccessStatuses.Selected
                            ? groupModel.GroupName
                            : null,
                        withType);
                case "User":
                    return DisplayText(
                        Displays.Users(),
                        Id != 0
                            ? SiteInfo.User(Id)?.FullName()
                            : null,
                        withType);
                default:
                    var column = SiteSettings?.GetColumn(Name);
                    return DisplayText(Displays.Column(), column?.LabelText, withType);
            }
        }

        private ControlData DisplayText(string title, string name, bool withType)
        {
            return new ControlData("[" + title +
                (Id != 0
                    ? " " + Id
                    : string.Empty) +
                "]" +
                (name != null
                    ? " " + name
                    : string.Empty) +
                (withType
                    ? " - [" + DisplayTypeName() + "]"
                    : string.Empty));
        }

        private string DisplayTypeName()
        {
            var permissionType = Type.ToLong();
            return Parameters.Permissions.Pattern.ContainsValue(permissionType)
                ? Displays.Get(Parameters.Permissions.Pattern.First(o =>
                    o.Value == permissionType).Key)
                : Displays.Special();
        }
    }
}