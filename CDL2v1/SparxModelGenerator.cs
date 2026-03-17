// <auto-gen>
//=======================================================================
// <copyright file="SparxModelGenerator.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>GitHub Copilot</author>
// <creation-date>2025-03-13</creation-date>
// 
// <summary>
//   Generates XMI 2.1 format models from C# classes for import into Sparx Enterprise Architect.
// </summary>
//=======================================================================
// </auto-gen>

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml;

namespace CDL2v1 {
   /// <summary>
   /// Generates XMI 2.1 format models that can be imported into Sparx Enterprise Architect.
   /// </summary>
   public class SparxModelGenerator : IDisposable {
      private readonly XmlWriter writer;
      private int elementIdCounter = 1;
      private readonly Dictionary<Type,string> typeIds = [];

      public SparxModelGenerator(string outputPath) {
         XmlWriterSettings settings = new XmlWriterSettings {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8
         };
         writer = XmlWriter.Create(outputPath,settings);
      }

      /// <summary>
      /// Generates XMI 1.1 model for all types in the specified namespace (Sparx EA compatible).
      /// </summary>
      public void GenerateModel(Assembly assembly,string namespaceName) {
         try {
            // Get all types from the namespace
            Type[] allTypes = assembly.GetTypes()
               .Where(t => t.Namespace == namespaceName && !t.IsNested)
               .OrderBy(t => t.Name)
               .ToArray();

            // Filter to only types defined in SyntaxTree.cs
            Type[] syntaxTreeTypes = allTypes
               .Where(t => {
                  string? fileName = t.GetCustomAttributes<System.Runtime.CompilerServices.CompilerGeneratedAttribute>().Any() 
                     ? null 
                     : t.Module.Name;
                  // Check if type is likely from SyntaxTree based on interfaces or base classes
                  return IsFromSyntaxTree(t);
               })
               .ToArray();

            // Collect referenced types (base classes, interfaces, property types not in SyntaxTree)
            HashSet<Type> referencedTypes = [];
            foreach (Type type in syntaxTreeTypes) CollectReferencedTypes(type,referencedTypes,allTypes);

            Debug.WriteLine($"Found {syntaxTreeTypes.Length} SyntaxTree types and {referencedTypes.Count} referenced types");

            writer.WriteStartDocument();
            writer.WriteStartElement("XMI");
            writer.WriteAttributeString("xmi.version","1.1");
            writer.WriteRaw(" xmlns:UML=\"omg.org/UML1.3\"");

            writer.WriteStartElement("XMI.header");
            writer.WriteStartElement("XMI.documentation");
            writer.WriteStartElement("XMI.exporter");
            writer.WriteString("C# to XMI Converter");
            writer.WriteEndElement();
            writer.WriteStartElement("XMI.exporterVersion");
            writer.WriteString("1.0");
            writer.WriteEndElement();
            writer.WriteEndElement(); // XMI.documentation
            writer.WriteEndElement(); // XMI.header

            writer.WriteStartElement("XMI.content");

            writer.WriteStartElement("UML","Model","omg.org/UML1.3");
            writer.WriteAttributeString("name",namespaceName);
            writer.WriteAttributeString("xmi.id","model_1");

            writer.WriteStartElement("UML","Namespace.ownedElement","omg.org/UML1.3");

            // Generate SyntaxTree package
            writer.WriteStartElement("UML","Package","omg.org/UML1.3");
            writer.WriteAttributeString("name","SyntaxTree");
            writer.WriteAttributeString("xmi.id","pkg_syntaxtree");
            writer.WriteStartElement("UML","Namespace.ownedElement","omg.org/UML1.3");

            int count = 0;
            foreach (Type type in syntaxTreeTypes) {
               try {
                  GenerateType(type);
                  count++;
                  if (count % 10 == 0) Debug.WriteLine($"Processed {count} SyntaxTree types...");
               } catch (Exception ex) {
                  Debug.WriteLine($"ERROR processing type {type.Name}: {ex.Message}");
               }
            }

            writer.WriteEndElement(); // Namespace.ownedElement
            writer.WriteEndElement(); // Package SyntaxTree

            // Generate Referenced Types package if any
            if (referencedTypes.Any()) {
               writer.WriteStartElement("UML","Package","omg.org/UML1.3");
               writer.WriteAttributeString("name","ReferencedTypes");
               writer.WriteAttributeString("xmi.id","pkg_referenced");
               writer.WriteStartElement("UML","Namespace.ownedElement","omg.org/UML1.3");

               foreach (Type type in referencedTypes.OrderBy(t => t.Name)) {
                  try {
                     GenerateType(type);
                     count++;
                  } catch (Exception ex) {
                     Debug.WriteLine($"ERROR processing referenced type {type.Name}: {ex.Message}");
                  }
               }

               writer.WriteEndElement(); // Namespace.ownedElement
               writer.WriteEndElement(); // Package Referenced
            }

            Debug.WriteLine($"Successfully processed {count} total types");

            writer.WriteEndElement(); // Namespace.ownedElement
            writer.WriteEndElement(); // Model
            writer.WriteEndElement(); // XMI.content
            writer.WriteEndElement(); // XMI
            writer.WriteEndDocument();

            Debug.WriteLine("XMI document completed");
         } catch (Exception ex) {
            Debug.WriteLine($"ERROR in GenerateModel: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
         }
      }

      private static bool IsFromSyntaxTree(Type type) {
         // Include all subclasses of NamedElement (includes CDL2Object, Container, Algorithm, Var, Const, LIST, Affix, Local, etc.)
         Type? namedElementType = type.Assembly.GetTypes().FirstOrDefault(t => t.Name == "NamedElement");
         if (namedElementType != null && namedElementType.IsAssignableFrom(type)) return true;

         // Include control flow structures (not subclasses of NamedElement)
         if (type.Name == "Group" || type.Name == "Alternative") return true;
         if (type.Name == "Call" || type.Name == "LastCall") return true;

         // Include element types (implement IElement but not NamedElement)
         if (type.Name == "INT" || type.Name == "FLOAT" || type.Name == "STRING") return true;

         // Include ID type
         if (type.Name == "ID") return true;

         return false;
      }

      private void CollectReferencedTypes(Type type,HashSet<Type> referenced,Type[] allTypes) {
         // Add base type if in namespace but not already processed
         if (type.BaseType != null && 
             type.BaseType != typeof(object) && 
             allTypes.Contains(type.BaseType) &&
             !IsFromSyntaxTree(type.BaseType)) {
            referenced.Add(type.BaseType);
         }

         // Add interfaces
         foreach (Type iface in type.GetInterfaces()) {
            if (allTypes.Contains(iface) && !IsFromSyntaxTree(iface)) {
               referenced.Add(iface);
            }
         }

         // Add property/field types that are in the namespace
         foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
            Type propType = prop.PropertyType;
            if (propType.IsGenericType) propType = propType.GetGenericTypeDefinition();
            if (allTypes.Contains(propType) && !IsFromSyntaxTree(propType)) {
               referenced.Add(propType);
            }
         }
      }

      private void GenerateType(Type type) {
         string id = GetTypeId(type);
         string elementType = type.IsInterface ? "Interface" : type.IsEnum ? "Enumeration" : type.IsClass ? "Class" : "DataType";

         writer.WriteStartElement("UML",elementType,"omg.org/UML1.3");
         writer.WriteAttributeString("name",type.Name);
         writer.WriteAttributeString("xmi.id",id);
         writer.WriteAttributeString("visibility","public");

         if (type.IsAbstract && type.IsClass) writer.WriteAttributeString("isAbstract","true");

         if (type.BaseType != null && type.BaseType != typeof(object)) {
            writer.WriteStartElement("UML","GeneralizableElement.generalization","omg.org/UML1.3");
            GenerateGeneralization(type.BaseType);
            writer.WriteEndElement();
         }

         PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
         FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
         MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).ToArray();

         if (props.Length > 0 || fields.Length > 0 || methods.Length > 0) {
            writer.WriteStartElement("UML","Classifier.feature","omg.org/UML1.3");

            foreach (PropertyInfo prop in props) GenerateProperty(prop);
            foreach (FieldInfo field in fields) GenerateField(field);
            foreach (MethodInfo method in methods) GenerateMethod(method);

            writer.WriteEndElement();
         }

         writer.WriteEndElement(); // Class/Interface/etc
      }

      private void GenerateGeneralization(Type baseType) {
         writer.WriteStartElement("UML","Generalization","omg.org/UML1.3");
         writer.WriteAttributeString("xmi.id",$"gen_{elementIdCounter++}");
         writer.WriteAttributeString("xmi.idref",GetTypeId(baseType));
         writer.WriteEndElement();
      }

      private void GenerateProperty(PropertyInfo prop) {
         writer.WriteStartElement("UML","Attribute","omg.org/UML1.3");
         writer.WriteAttributeString("name",prop.Name);
         writer.WriteAttributeString("xmi.id",$"prop_{elementIdCounter++}");
         writer.WriteAttributeString("visibility","public");

         writer.WriteStartElement("UML","StructuralFeature.type","omg.org/UML1.3");
         writer.WriteStartElement("UML","DataType","omg.org/UML1.3");
         writer.WriteAttributeString("name",GetTypeName(prop.PropertyType));
         writer.WriteEndElement();
         writer.WriteEndElement();

         writer.WriteEndElement();
      }

      private void GenerateField(FieldInfo field) {
         writer.WriteStartElement("UML","Attribute","omg.org/UML1.3");
         writer.WriteAttributeString("name",field.Name);
         writer.WriteAttributeString("xmi.id",$"field_{elementIdCounter++}");
         writer.WriteAttributeString("visibility","public");

         writer.WriteStartElement("UML","StructuralFeature.type","omg.org/UML1.3");
         writer.WriteStartElement("UML","DataType","omg.org/UML1.3");
         writer.WriteAttributeString("name",GetTypeName(field.FieldType));
         writer.WriteEndElement();
         writer.WriteEndElement();

         writer.WriteEndElement();
      }

      private void GenerateMethod(MethodInfo method) {
         writer.WriteStartElement("UML","Operation","omg.org/UML1.3");
         writer.WriteAttributeString("name",method.Name);
         writer.WriteAttributeString("xmi.id",$"op_{elementIdCounter++}");
         writer.WriteAttributeString("visibility","public");

         ParameterInfo[] parameters = method.GetParameters();
         if (parameters.Length > 0 || method.ReturnType != typeof(void)) {
            writer.WriteStartElement("UML","BehavioralFeature.parameter","omg.org/UML1.3");

            foreach (ParameterInfo param in parameters) {
               writer.WriteStartElement("UML","Parameter","omg.org/UML1.3");
               writer.WriteAttributeString("name",param.Name ?? "");
               writer.WriteAttributeString("xmi.id",$"param_{elementIdCounter++}");
               writer.WriteAttributeString("kind","in");

               writer.WriteStartElement("UML","Parameter.type","omg.org/UML1.3");
               writer.WriteStartElement("UML","DataType","omg.org/UML1.3");
               writer.WriteAttributeString("name",GetTypeName(param.ParameterType));
               writer.WriteEndElement();
               writer.WriteEndElement();

               writer.WriteEndElement();
            }

            if (method.ReturnType != typeof(void)) {
               writer.WriteStartElement("UML","Parameter","omg.org/UML1.3");
               writer.WriteAttributeString("xmi.id",$"return_{elementIdCounter++}");
               writer.WriteAttributeString("kind","return");

               writer.WriteStartElement("UML","Parameter.type","omg.org/UML1.3");
               writer.WriteStartElement("UML","DataType","omg.org/UML1.3");
               writer.WriteAttributeString("name",GetTypeName(method.ReturnType));
               writer.WriteEndElement();
               writer.WriteEndElement();

               writer.WriteEndElement();
            }

            writer.WriteEndElement();
         }

         writer.WriteEndElement();
      }

      private string GetTypeName(Type type) {
         if (type.IsGenericType) {
            string genericName = type.Name.Substring(0,type.Name.IndexOf('`'));
            string typeArgs = string.Join(",",type.GetGenericArguments().Select(t => GetTypeName(t)));
            return $"{genericName}<{typeArgs}>";
         } else {
            return type.Name;
         }
      }

      private string GetTypeId(Type type) {
         if (!typeIds.TryGetValue(type,out string? id)) {
            id = $"type_{type.Name}_{elementIdCounter++}";
            typeIds[type] = id;
         }
         return id;
      }

      public void Close() {
         writer.Flush();
         writer.Close();
      }

      public void Dispose() {
         writer?.Flush();
         writer?.Close();
         GC.SuppressFinalize(this);
      }

      /// <summary>
      /// Generates XMI model for CDL2v1 namespace and saves it to the specified file.
      /// </summary>
      public static void GenerateCDL2Model(string outputPath) {
         try {
            Assembly assembly = typeof(CDL2Object).Assembly;
            using (SparxModelGenerator generator = new SparxModelGenerator(outputPath)) {
               generator.GenerateModel(assembly,"CDL2v1");
               generator.writer.Flush();
            }
         } catch (Exception ex) {
            Debug.WriteLine($"ERROR in GenerateCDL2Model: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
         }
      }
   }
}
