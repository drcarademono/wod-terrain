using IniParser.Model;

public interface IniSerializable
{
    string[] GetSerializedSection(string comment = "");
    void DeserializeSection(string sectionName, KeyDataCollection keyData);
}
