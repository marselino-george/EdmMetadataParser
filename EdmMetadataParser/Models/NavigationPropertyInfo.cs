using System.Collections.Generic;
using System.Linq;

namespace EdmMetadataParser.Models
{
    /// <summary>
    /// Example:
    /// NavigationProperty Name="ReleasedProductMaster" Type="Microsoft.Dynamics.DataEntities.ReleasedProductMasterV2" Nullable="false" Partner="ReleasedProductVariants"
    /// 
    /// To put it simply, if you're on an entity and you navigate using `ReleasedProductMaster`, you'd get to an entity of type `ReleasedProductMasterV2`.
    /// If you're on that `ReleasedProductMasterV2` entity, you should be able to navigate back to the original entity using a property named 
    /// `ReleasedProductVariants`. This sets up a two-way association or bidirectional relationship between the two entities.
    /// </summary>
    public class NavigationPropertyInfo : IPropertyBasicAttributes
    {
        /// <summary>
        /// This is the name of the navigation property as it will be seen on the entity which this 
        /// definition belongs to. Based on the example, the property name would be `ReleasedProductMaster`.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// This specifies the entity type that the navigation property points to.
        /// Based on the example, the `ReleasedProductMaster` property will point to an entity of type 
        /// `Microsoft.Dynamics.DataEntities.ReleasedProductMasterV2`.
        /// </summary>
        public string Type { get; set; }

        public bool? Nullable { get; set; }

        /// <summary>
        /// The `Partner` attribute `ReleasedProductVariants` implies that on the 
        /// `Microsoft.Dynamics.DataEntities.ReleasedProductMasterV2` entity, there's a reverse 
        /// navigation property named `ReleasedProductVariants` that points back to the entity 
        /// where `ReleasedProductMaster` is defined.
        /// </summary>
        public string Partner { get; set; }

        public List<ReferentialConstraint> ReferentialConstraints { get; set; }

        public NavigationPropertyInfo()
        {
            ReferentialConstraints = new List<ReferentialConstraint>();
        }

        public string TypeToEntityName()
        {
            if (string.IsNullOrEmpty(Type)) return Type;

            return Type.Replace("Collection(", "").Replace(")", "")
            ?.Split('.') // Example: Microsoft.Dynamics.DataEntities.FiscalCalendarEntity
            ?.LastOrDefault(); // Returns: FiscalCalendarEntity
        }

        public override string ToString()
        {
            return Name.ToString();
        }
    }
}
