using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
public class WpcutSchema
{
    [DataMember(Name = "shortcutname")]
    public string ShortcutName { get; set; }

    [DataMember(Name = "shortcutuuid")]
    public string ShortcutUuid { get; set; }

    [DataMember(Name = "shortcuticon")]
    public string ShortcutIcon { get; set; }

    [DataMember(Name = "shortcutactions")]
    public List<WpcutAction> ShortcutActions { get; set; } = new List<WpcutAction>();
}

[DataContract]
public class WpcutAction
{
    [DataMember(Name = "type")]
    public string Type { get; set; }

    [DataMember(Name = "dialog")]
    public string Dialog { get; set; }

    [DataMember(Name = "writefilepath")]
    public string WriteFilePath { get; set; }

    [DataMember(Name = "writefilecontent")]
    public string WriteFileContent { get; set; }
}