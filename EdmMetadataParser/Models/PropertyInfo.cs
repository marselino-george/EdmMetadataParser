using System.Collections.Generic;

namespace EdmMetadataParser.Models
{
    public class PropertyInfo : IPropertyBasicAttributes
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<AnnotationInfo> Annotations { get; set; }
    }
}
