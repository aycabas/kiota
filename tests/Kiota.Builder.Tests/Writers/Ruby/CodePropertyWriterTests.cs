using System;
using System.IO;
using System.Linq;
using Kiota.Builder.Extensions;
using Xunit;

namespace Kiota.Builder.Writers.Ruby.Tests {
    public class CodePropertyWriterTests: IDisposable {
        private const string DefaultPath = "./";
        private const string DefaultName = "name";
        private readonly StringWriter tw;
        private readonly LanguageWriter writer;
        private readonly CodeProperty property;
        private readonly CodeClass parentClass;
        private readonly CodeClass EmptyClass;
        private readonly CodeProperty emptyProperty;
        private const string PropertyName = "propertyName";
        private const string TypeName = "Somecustomtype";
        private const string RootNamespaceName = "RootNamespace";
        public CodePropertyWriterTests() {
            writer = LanguageWriter.GetLanguageWriter(GenerationLanguage.Ruby, DefaultPath, DefaultName);
            tw = new StringWriter();
            writer.SetTextWriter(tw);
            var emptyRoot = CodeNamespace.InitRootNamespace();
            EmptyClass = new CodeClass(emptyRoot) {
                Name = "emptyClass"
            };
            emptyProperty = new CodeProperty(EmptyClass) {
                Name = PropertyName,
            };
            emptyProperty.Type = new CodeType(emptyProperty) {
                Name = TypeName
            };
            EmptyClass.AddProperty(emptyProperty);
            
            var root = CodeNamespace.InitRootNamespace();
            root.Name = RootNamespaceName;
            parentClass = new CodeClass(root) {
                Name = "parentClass"
            };
            root.AddClass(parentClass);
            property = new CodeProperty(parentClass) {
                Name = PropertyName,
            };
            property.Type = new CodeType(property) {
                Name = TypeName,
                TypeDefinition = parentClass
            };
            parentClass.AddProperty(property);
        }
        public void Dispose() {
            tw?.Dispose();
            GC.SuppressFinalize(this);
        }
        [Fact]
        public void WritesRequestBuilder() {
            property.PropertyKind = CodePropertyKind.RequestBuilder;
            writer.Write(property);
            var result = tw.ToString();
            Assert.Contains($"def {PropertyName.ToSnakeCase()}", result);
            Assert.Contains($"{RootNamespaceName}::{TypeName}.new", result);
            Assert.Contains("http_core", result);
            Assert.Contains("path_segment", result);
        }
        [Fact]
        public void WritesRequestBuilderWithoutNamespace() {
            emptyProperty.PropertyKind = CodePropertyKind.RequestBuilder;
            writer.Write(emptyProperty);
            var result = tw.ToString();
            Assert.Contains($"def {PropertyName.ToSnakeCase()}", result);
            Assert.Contains($"{TypeName}.new", result);
            Assert.Contains("http_core", result);
            Assert.Contains("path_segment", result);
            Assert.DoesNotContain($"::{TypeName}.new", result);
        }
        [Fact]
        public void WritesCustomProperty() {
            property.PropertyKind = CodePropertyKind.Custom;
            writer.Write(property);
            var result = tw.ToString();
            Assert.Contains($"@{PropertyName.ToSnakeCase()}", result);
        }
    }
}
