using System.Collections.Generic;

namespace EdmMetadataParser.Models
{
    public class EntityRelationshipInfo
    {
        public string? FromEntityType { get; set; }
        public string? NavigationProperty { get; set; } // Evaluate: Might Convert this to NavigationPropertyInfo.
        public string? ToEntityType { get; set; }
        public List<string>? Keys { get; set; }
    }
}
