using System.Collections.Generic;

namespace EdmMetadataParser.Models
{
    public class EntityInfo
    {
        public string Name { get; set; }
        public List<PropertyInfo> Properties { get; set; }
        public List<NavigationPropertyInfo> NavigationProperties { get; set; }
        public List<string> Keys { get; set; }
    }
}
