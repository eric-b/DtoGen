using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace DtoGen
{
    /// <summary>
    /// Entity class generator based on SQL result.
    /// </summary>
    class Program
    {
        /* dtogen.exe -name "class name" -sql "query" -cn "connection string name"
         * */

        static void Main(string[] args)
        {
            const string allowedPropertyNameCharacters = "azertyuiopmlkjhgfdsqwxcvbn_123456789";
            try
            {

                string entityName = null, sql = null, connectionStringName = null, entityNs = null;

                #region Parse la ligne de commande
                const string argName = "name", argSql = "sql", argCn = "cn", argNs = "ns";
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].StartsWith("-"))
                    {
                        var argument = args[i].Substring(1).ToLower();
                        switch (argument)
                        {
                            case argName:
                                entityName = args[++i];
                                break;
                            case argSql:
                                sql = args[++i];
                                break;
                            case argCn:
                                connectionStringName = args[++i];
                                break;
                            case argNs:
                                entityNs = args[++i];
                                break;
                            default:
                                throw new ArgumentException("Unexpected argument: " + argument, argument);
                        }
                    }
                }

                if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(sql) || string.IsNullOrEmpty(connectionStringName))
                {
                    Console.WriteLine(string.Format(@"
Generates an entity class based on a SQL query result.

Syntax:
{0} -name ""[class name]"" -sql ""[SQL query]"" -cn ""[connection string name]"" -ns ""[namespace]""", Path.GetFileName(Environment.GetCommandLineArgs()[0])));
                    return;
                }
                #endregion

                #region Initialize generated class
                var targetUnit = new CodeCompileUnit();
                var targetClass = new CodeTypeDeclaration(entityName);
                targetClass.IsClass = true;
                targetClass.TypeAttributes = System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Sealed;
                var codeNs = new CodeNamespace();
                if (!string.IsNullOrEmpty(entityNs))
                    codeNs.Name = entityNs;
                codeNs.Imports.Add(new CodeNamespaceImport("System"));
                codeNs.Types.Add(targetClass);
                targetUnit.Namespaces.Add(codeNs);
                codeNs.Comments.Add(new CodeCommentStatement(string.Format(@"Generated class from query: 
{0}", sql), false));
                #endregion


                var propertyNames = new HashSet<string>();
                Database db;
                try
                {
                    db = Microsoft.Practices.EnterpriseLibrary.Common.Configuration.EnterpriseLibraryContainer.Current.GetInstance<Database>(connectionStringName);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(string.Format("Connection string name invalid or unknown. Check parameter {0} or config file (connection string name: {1}).", argCn, connectionStringName), argCn, ex);
                }
                using (var cmd = db.GetSqlStringCommand(sql))
                {
                    Console.WriteLine("Please wait...");
                    using (var reader = db.ExecuteReader(cmd))
                    {
                        var schema = reader.GetSchemaTable();

                        for (int i = 0; i < schema.Rows.Count; i++)
                        {
                            var name = (string)schema.Rows[i]["ColumnName"];
                            var type = (Type)schema.Rows[i]["DataType"];
                            var allowNull = (bool)schema.Rows[i]["AllowDBNull"];
                            if (allowNull && !type.IsClass)
                                type = Type.GetType(string.Format("System.Nullable`1[{0}]", type.FullName), true);
                            if (string.IsNullOrEmpty(name))
                            {
                                Console.WriteLine(string.Format("WARNING: column without name at index {0} !", i));
                                continue;
                            }
                            if (propertyNames.Contains(name))
                            {
                                Console.WriteLine(string.Format("WARNING: duplicate column name ignored: {0}", name));
                                continue;
                            }
                            // We use CodeSnippetTypeMember since auto-implemented properties are not supported by CodeDOM... (supports only "CSharp" provider)

                            var firstCharacter = name.First();
                            if (char.IsDigit(firstCharacter))
                            {
                                Console.WriteLine(string.Format("WARNING: invalid column name: {0}", name));
                            }
                            else if (name.ToLower().FirstOrDefault(t => !allowedPropertyNameCharacters.Contains(t)) != default(char))
                            {
                                Console.WriteLine(string.Format("WARNING: invalid column name: {0}", name));
                            }

                            targetClass.Members.Add(new CodeMemberField()
                            {
                                Name = string.Format("{1}{0} {{ get; set; }} //", name, (char.IsUpper(firstCharacter) ? null : "@")), 
                                Type = new CodeTypeReference(type),
                                Attributes = MemberAttributes.Public | MemberAttributes.Final

                            });
                            propertyNames.Add(name);
                        }
                    }
                }

                #region Generates class
                if (propertyNames.Count != 0)
                {
                    var codeProvider = CodeDomProvider.CreateProvider("CSharp");
                    var options = new CodeGeneratorOptions()
                    {
                        BracingStyle = "C"
                    };
                    var filename = string.Format("{0}.cs", entityName);
                    using (var writer = new StreamWriter(filename))
                    {
                        codeProvider.GenerateCodeFromCompileUnit(targetUnit, writer, options);
                    }

                    Console.WriteLine(string.Format("Generated class {0}: {1} propertie(s) mapped.", filename, propertyNames.Count));
                }
                else
                {
                    Console.WriteLine("ERROR: Nothing was generated. Check your query.");
                }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                Console.ReadKey(true);
            }
        }
    }
}
