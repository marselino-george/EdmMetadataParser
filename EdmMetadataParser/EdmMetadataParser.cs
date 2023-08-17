using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EdmMetadataParser.Models;

namespace EdmMetadataParser
{
    public sealed class EdmMetadataXmlParser
    {
        private readonly XNamespace _ns = "http://docs.oasis-open.org/odata/ns/edm";
        private readonly Lazy<XDocument> _doc;

        public EdmMetadataXmlParser(string filePath)
        {
            _doc = new Lazy<XDocument>(() => XDocument.Load(filePath));
        }

        public EdmMetadataXmlParser(string xmlData, bool isFileName)
        {
            _doc = isFileName ? new Lazy<XDocument>(() => XDocument.Load(xmlData)) : new Lazy<XDocument>(() => XDocument.Parse(xmlData));
        }

        public EdmMetadataXmlParser(Stream xmlStream)
        {
            _doc = new Lazy<XDocument>(() => XDocument.Load(xmlStream));
        }

        public List<EntityRelationshipInfo> ParseXmlToEntityRelationships()
        {
            var entityRelationships = new List<EntityRelationshipInfo>();

            var entityTypes = _doc.Value.Descendants(_ns + "EntityType");

            foreach (var entityType in entityTypes)
            {
                var fromEntityType = entityType.Attribute("Name")?.Value;

                // Consider BaseType elements which typically represent inheritance in an EDMX.
                var baseType = entityType.Attribute("BaseType")?.Value;
                if (baseType != null)
                {
                    var relationshipInfo = new EntityRelationshipInfo
                    {
                        FromEntityType = fromEntityType,
                        NavigationProperty = "InheritsFrom",
                        ToEntityType = baseType.Split('.').Last()
                    };

                    entityRelationships.Add(relationshipInfo);
                }

                var navigationProperties = entityType.Elements(_ns + "NavigationProperty");

                // New: process the keys
                var keys = entityType.Element(_ns + "Key")?.Elements(_ns + "PropertyRef")
                    .Select(pr => pr.Attribute("Name")?.Value);


                foreach (var navigationProperty in navigationProperties)
                {
                    var relationshipInfo = new EntityRelationshipInfo
                    {
                        FromEntityType = fromEntityType,
                        NavigationProperty = navigationProperty.Attribute("Name")?.Value,
                        ToEntityType = navigationProperty.Attribute("Type")?.Value
                            ?.Replace("Collection(", "").Replace(")", "")
                            ?.Split('.') // Example: Microsoft.Dynamics.DataEntities.FiscalCalendarEntity
                            ?.LastOrDefault(), // Returns: FiscalCalendarEntity
                        Keys = keys?.ToList() // New: add the keys
                    };

                    entityRelationships.Add(relationshipInfo);

                    // New: process the foreign keys
                    var foreignKeys = navigationProperty.Elements(_ns + "ReferentialConstraint")
                        .Select(rc => rc.Attribute("Property")?.Value);
                    foreach (var foreignKey in foreignKeys)
                    {
                        entityRelationships.Add(new EntityRelationshipInfo
                        {
                            FromEntityType = fromEntityType,
                            NavigationProperty = foreignKey,
                            ToEntityType = navigationProperty.Attribute("Type")?.Value?.Replace("Collection(", "").Replace(")", "")
                            ?.Split('.')?.LastOrDefault()
                        });
                    }
                }

                // Consider ReferentialConstraint elements which typically represent foreign key relationships.
                var referentialConstraints = entityType.Elements(_ns + "ReferentialConstraint");

                foreach (var referentialConstraint in referentialConstraints)
                {
                    var toEntityType = referentialConstraint.Attribute("ReferencedEntityType")?.Value?.Split('.')?.LastOrDefault();
                    if (string.IsNullOrEmpty(toEntityType))
                    {
                        continue;
                    }

                    var relationshipInfo = new EntityRelationshipInfo
                    {
                        FromEntityType = fromEntityType,
                        // Referential constraints are not navigation properties so this field is null.
                        NavigationProperty = null,
                        ToEntityType = toEntityType
                    };

                    entityRelationships.Add(relationshipInfo);
                }
            }

            return entityRelationships;
        }

        public List<List<EntityRelationshipInfo>> FindPaths(string startEntity, string endEntity, int maxDepth, List<EntityRelationshipInfo> entityRelationships)
        {
            List<List<EntityRelationshipInfo>> result = new List<List<EntityRelationshipInfo>>();
            HashSet<string> visitedEntities = new HashSet<string>();
            DFS(startEntity, endEntity, maxDepth, new List<EntityRelationshipInfo>(), visitedEntities, result, entityRelationships);
            return result;
        }

        private void DFS(string currentEntity, string endEntity, int depth, List<EntityRelationshipInfo> currentPath, HashSet<string> visitedEntities, List<List<EntityRelationshipInfo>> result, List<EntityRelationshipInfo> entityRelationships)
        {
            if (depth < 0)
            {
                return;
            }

            visitedEntities.Add(currentEntity);

            if (currentEntity == endEntity)
            {
                result.Add(new List<EntityRelationshipInfo>(currentPath));
            }

            if (depth > 0)
            {
                var relatedEntities = entityRelationships.Where(er => er.FromEntityType == currentEntity || er.ToEntityType == currentEntity).ToList();
                foreach (var entity in relatedEntities)
                {
                    string nextEntity = entity.FromEntityType == currentEntity ? entity.ToEntityType : entity.FromEntityType;

                    if (!visitedEntities.Contains(nextEntity))
                    {
                        currentPath.Add(entity);
                        DFS(nextEntity, endEntity, depth - 1, currentPath, visitedEntities, result, entityRelationships);
                        currentPath.RemoveAt(currentPath.Count - 1);
                    }
                }
            }

            visitedEntities.Remove(currentEntity);
        }

        public IEnumerable<EntityInfo> GetAllEntitiesInfo(params string[] entityNames)
        {
            var entityTypes = _doc.Value.Descendants(_ns + "EntityType");

            foreach (var entityType in entityTypes)
            {
                var entityName = entityType.Attribute("Name")?.Value;

                if (entityNames != null && entityNames.Any() && !entityNames.Any(entity => string.Equals(entity, entityName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var entityInfo = new EntityInfo
                {
                    Name = entityName,
                    Properties = new List<PropertyInfo>(),
                    NavigationProperties = new List<NavigationPropertyInfo>(),
                    Keys = entityType.Element(_ns + "Key")?.Elements(_ns + "PropertyRef")
                        .Select(pr => pr.Attribute("Name")?.Value).ToList() ?? new List<string>()
                };

                var propertyElements = entityType.Elements(_ns + "Property");
                entityInfo.Properties.AddRange(GetEntityProperties(propertyElements));

                var navigationPropertyElements = entityType.Elements(_ns + "NavigationProperty");
                entityInfo.NavigationProperties.AddRange(GetNavigationProperties(navigationPropertyElements));
                yield return entityInfo;
            }
        }

        public IEnumerable<PropertyInfo> GetEntityProperties(IEnumerable<XElement> propertyElements)
        {
            foreach (var property in propertyElements)
            {
                List<AnnotationInfo> annotations = new();
                annotations.AddRange(from annotation in property.Descendants().Where(e => e.Name.LocalName == "Annotation") select GetAnnotation(annotation));

                yield return new PropertyInfo
                {
                    Name = property.Attribute("Name")?.Value,
                    Type = property.Attribute("Type")?.Value,
                    Annotations = annotations
                };
            }
        }

        public IEnumerable<NavigationPropertyInfo> GetNavigationProperties(IEnumerable<XElement> navigationPropertyElements)
        {
            foreach (var navigationProperty in navigationPropertyElements)
            {
                var nullableAttrRaw = navigationProperty.Attribute("Nullable")?.Value;
                NavigationPropertyInfo navigationPropertyInfo = new()
                {
                    Name = navigationProperty.Attribute("Name")?.Value,
                    Type = navigationProperty.Attribute("Type")?.Value,
                    Nullable = !string.IsNullOrEmpty(nullableAttrRaw) && string.Equals(nullableAttrRaw, "true", StringComparison.InvariantCultureIgnoreCase),
                    Partner = navigationProperty.Attribute("Partner")?.Value,
                };

                var referentialConstraints = navigationProperty.Elements(_ns + "ReferentialConstraint");
                if (referentialConstraints.Any())
                {
                    foreach (var referentialConstraintProp in referentialConstraints)
                    {
                        ReferentialConstraint referentialConstraint = new()
                        {
                            Property = referentialConstraintProp.Attribute("Property")?.Value,
                            ReferencedProperty = referentialConstraintProp.Attribute("ReferencedProperty")?.Value
                        };

                        navigationPropertyInfo.ReferentialConstraints.Add(referentialConstraint);
                    }

                }

                yield return navigationPropertyInfo;
            }
        }

        public AnnotationInfo GetAnnotation(XElement annotationElement)
        {
            var annotationType = GetAnnotationType(annotationElement);

            return new AnnotationInfo
            {
                Term = annotationElement.Attribute("Term")?.Value,
                AnnotationType = annotationType
            };
        }

        public AnnotationType GetAnnotationType(XElement annotationElement)
        {
            if (annotationElement.Name.LocalName != "Annotation")
            {
                throw new ArgumentException("Element is not an Annotation");
            }

            AnnotationRegularType getAnnotationRegularType(string attributeName)
            {
                return attributeName switch
                {
                    "String" => AnnotationRegularType.String,
                    "Bool" => AnnotationRegularType.Bool,
                    "EnumMember" => AnnotationRegularType.Enum,
                    _ => throw new ArgumentException($"Unknown annotation type: {attributeName}")
                };
            }

            AXType getAXType(string attributeName)
            {
                return attributeName switch
                {
                    "String" => AXType.String,
                    "Real" => AXType.Real,
                    "Int32" => AXType.Int32,
                    "Int64" => AXType.Int64,
                    "Enum" => AXType.Enum,
                    "UtcDateTime" => AXType.UtcDateTime,
                    "Guid" => AXType.Guid,
                    "Date" => AXType.Date,
                    "Time" => AXType.Time,
                    "Container" => AXType.Container,
                    _ => throw new ArgumentException($"Unknown annotation type: {attributeName}")
                };
            }

            string attributeName = annotationElement.Attributes().FirstOrDefault(item => !string.Equals(item.Name.LocalName, "Term", StringComparison.OrdinalIgnoreCase))?.Name.LocalName;
            if (!string.IsNullOrEmpty(attributeName))
            {
                return new AnnotationType(getAnnotationRegularType(attributeName), null);
            }

            // Check for EnumMember content
            var enumMemberElement = annotationElement.Elements().FirstOrDefault(e => e.Name.LocalName == "EnumMember");
            var enumMemberValue = enumMemberElement?.Value;
            if (!string.IsNullOrEmpty(enumMemberValue))
            {
                attributeName = enumMemberValue.Split('/').Last();
                AXType axType = getAXType(attributeName);
                return new AnnotationType(AnnotationRegularType.Enum, axType);
            }

            throw new ArgumentException($"No AttributeName found {attributeName}");
        }
    }

}
