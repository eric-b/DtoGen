using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
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

                #region Command line parsing
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

                // Initialize generated class
                CodeTypeDeclaration targetClass;
                var targetUnit = Generator.CreateDefaultCompileUnit(entityNs, entityName, new CodeCommentStatement(string.Format(@"Generated class from query: {0}", sql), false), out targetClass);

                var cxString = ConfigurationManager.ConnectionStrings[connectionStringName];
                if (cxString == null)
                    throw new ArgumentException(string.Format("Connection string name invalid or unknown. Check parameter '{0}' or config file (connection string name: '{1}'). Case is sensitive!\r\nAvailable connection strings:\r\n:{2}", argCn, connectionStringName, string.Join(", ", ConfigurationManager.ConnectionStrings.OfType<ConnectionStringSettings>().Select(t => t.ConnectionString))));
                var dbFactory = DbProviderFactories.GetFactory(cxString.ProviderName);

                using (var cx = dbFactory.CreateConnection())
                {
                    cx.ConnectionString = cxString.ConnectionString;
                    cx.Open();
                    using (var cmd = cx.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.CommandType = System.Data.CommandType.Text;
                        Console.WriteLine("Please wait...");
                        using (var reader = cmd.ExecuteReader())
                        {
                            var generator = new Generator(new ConsoleTraceWriter());

                            generator.EmitMembers(reader, targetClass);
                        }
                    }
                }

                #region Generates class
                if (targetClass.Members.Count != 0)
                {
                    var filename = string.Format("{0}.cs", entityName);
                    using (var writer = new StreamWriter(filename))
                    {
                        Generator.GenerateCSCodeFromCompileUnit(targetClass, targetUnit, writer);
                    }

                    Console.WriteLine(string.Format("Generated class {0}: {1} propertie(s) mapped.", filename, targetClass.Members.Count));
                }
                else
                {
                    Console.WriteLine("ERROR: Nothing was generated. Check your query.");
                }
                #endregion
            }
            catch (Exception ex)
            {
                const string filename = "last-error.log";
                try
                {
                    File.WriteAllText("last-error.log",
                                      string.Format("{0}: {1}\r\n{2}", DateTime.Now, Environment.CommandLine, ex));
                    var path = new FileInfo(filename);
                    Console.WriteLine("{0}\r\nView full error details in :\r\n{1}", ex.Message, path.FullName);
                }
                catch
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            finally
            {
                Console.ReadKey(true);
            }
        }
    }
}
