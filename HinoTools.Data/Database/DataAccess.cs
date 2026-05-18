using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Linq;

namespace HinoTools.Data.Database
{
    public class DataAccess
    {
        public string ConnectionString { get; set; }

        public int ExecuteNonQuery(string query)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query;

                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public int ExecuteNonQuery(string query, params object[] parameters)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query;

                    string[] querySplit = query.Split(' ');
                    string[] paramNames = querySplit.Where(x => x.StartsWith("@")).ToArray();

                    if (parameters.Length == paramNames.Length)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            cmd.Parameters.AddWithValue(paramNames[i], parameters[i]);
                        }
                        return cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
            }
        }

        public DataTable ExecuteQuery(string query)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query;
                    using (MySqlDataAdapter dataAdapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dataTable = new DataTable();
                        dataAdapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        public object ExecuteScalarQuery(string query)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = query;

                    return cmd.ExecuteScalar();
                }
            }
        }
    }
}
