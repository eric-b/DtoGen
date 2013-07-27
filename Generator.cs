using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace DtoGen
{
    /// <summary>
    /// Code generator based on <see cref="IDataReader"/>.
    /// </summary>
    public class Generator
    {
        const string AllowedPropertyNameCharacters = "azertyuiopmlkjhgfdsqwxcvbn_123456789";
        private readonly ITraceWriter _traces;

        public Generator(ITraceWriter traces)
        {
            if (traces == null)
                throw new NullReferenceException("traces");
            _traces = traces;
        }

        /// <summary>
        /// Emits properties based on the schema of the specified <see cref="IDataReader"/>.
        /// </summary>
        /// <param name="reader">Data reader</param>
        /// <param name="targetClass">Target class.</param>
        /// <returns>Number of properties emitted</returns>
        public int EmitMembers(IDataReader reader, CodeTypeDeclaration targetClass)
        {
            var schema = reader.GetSchemaTable();
            var propertyNames = new HashSet<string>();
            int warningNumber = 0;
            var notifyWarnings = new List<int>();
            

            for (int i = 0; i < schema.Rows.Count; i++)
            {
                var name = (string)schema.Rows[i]["ColumnName"];
                var type = (Type)schema.Rows[i]["DataType"];
                var allowNull = (bool)schema.Rows[i]["AllowDBNull"];
                if (allowNull && !type.IsClass)
                    type = Type.GetType(string.Format("System.Nullable`1[{0}]", type.FullName), true);
                if (string.IsNullOrEmpty(name))
                {
                    notifyWarnings.Add(++warningNumber);
                    var msg = string.Format("WARNING {2}: column without name at index {0} (type: {1}) !", i, ToGenericTypeString(type), warningNumber);
                    targetClass.Comments.Add(new CodeCommentStatement(msg));
                    _traces.WriteLine(msg);
                    continue;
                }
                if (propertyNames.Contains(name))
                {
                    notifyWarnings.Add(++warningNumber);
                    var msg = string.Format("WARNING {3}: duplicate column name ignored: {0} (type: {1}, index: {2}).", name, ToGenericTypeString(type), i, warningNumber);
                    targetClass.Comments.Add(new CodeCommentStatement(msg));
                    _traces.WriteLine(msg);
                    continue;
                }
                // We use CodeSnippetTypeMember since auto-implemented properties are not supported by CodeDOM... (supports only "CSharp" provider)

                var firstCharacter = name.First();
                if (char.IsDigit(firstCharacter))
                {
                    notifyWarnings.Add(++warningNumber);
                    var msg = string.Format("WARNING {3}: invalid column name: {0} (type: {1}, index: {2}).", name, ToGenericTypeString(type), i, warningNumber);
                    targetClass.Comments.Add(new CodeCommentStatement(msg));
                    _traces.WriteLine(msg);
                }
                else if (name.ToLower().FirstOrDefault(t => !AllowedPropertyNameCharacters.Contains(t)) != default(char))
                {
                    notifyWarnings.Add(++warningNumber);
                    var msg = string.Format("WARNING {3}: invalid column name: {0} (type: {1}, index: {2}).", name, ToGenericTypeString(type), i, warningNumber);
                    targetClass.Comments.Add(new CodeCommentStatement(msg));
                    _traces.WriteLine(msg);
                }

                propertyNames.Add(name);
                targetClass.Members.Add(new CodeMemberField()
                {
                    Name = string.Format("{1}{0} {{ get; set; }} // index: {3}{2}", name, char.IsUpper(firstCharacter) ? null : "@", notifyWarnings.Count != 0 ? " - Before: see warning(s) " + string.Join(", ", notifyWarnings) : null, i),
                    Type = new CodeTypeReference(type),
                    Attributes = MemberAttributes.Public | MemberAttributes.Final
                });
                notifyWarnings.Clear();
            }
            return propertyNames.Count;
        }

        /// <summary>
        /// Creates a <see cref="CodeCompileUnit"/>.
        /// </summary>
        /// <param name="classNamespace">Namespace to use (optional)</param>
        /// <param name="className">Name of class to generate</param>
        /// <param name="comments">Optional comments</param>
        /// <param name="targetClass">Gets the generated class integrated into the <see cref="CodeCompileUnit"/> returned by this method.</param>
        /// <returns></returns>
        public static CodeCompileUnit CreateDefaultCompileUnit(string classNamespace, string className, CodeCommentStatement comments, out CodeTypeDeclaration targetClass)
        {
            if (string.IsNullOrEmpty(className))
                throw new ArgumentNullException("className");

            var targetUnit = new CodeCompileUnit();
            targetClass = new CodeTypeDeclaration(className)
                {
                    IsClass = true,
                    TypeAttributes = System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Sealed
                };
            var codeNs = new CodeNamespace();
            if (!string.IsNullOrEmpty(classNamespace))
                codeNs.Name = classNamespace;
            codeNs.Imports.Add(new CodeNamespaceImport("System"));
            codeNs.Types.Add(targetClass);
            targetUnit.Namespaces.Add(codeNs);
            if (comments != null)
                codeNs.Comments.Add(comments);

            return targetUnit;
        }

        /// <summary>
        /// Generated the final code with C# compiler.
        /// </summary>
        /// <param name="targetClass"></param>
        /// <param name="targetUnit"></param>
        /// <param name="writer"></param>
        public static void GenerateCSCodeFromCompileUnit(CodeTypeDeclaration targetClass, CodeCompileUnit targetUnit, TextWriter writer)
        {
            var codeProvider = CodeDomProvider.CreateProvider("CSharp");
            var options = new CodeGeneratorOptions()
            {
                BracingStyle = "C"
            };
            codeProvider.GenerateCodeFromCompileUnit(targetUnit, writer, options);
        }

        private static string ToGenericTypeString(Type t)
        {
            if (!t.IsGenericType)
                return t.Name;
            string genericTypeName = t.GetGenericTypeDefinition().Name;
            genericTypeName = genericTypeName.Substring(0,
                genericTypeName.IndexOf('`'));
            string genericArgs = string.Join(",",
                t.GetGenericArguments()
                    .Select(ToGenericTypeString).ToArray());
            return genericTypeName + "<" + genericArgs + ">";
        }
    }
}
