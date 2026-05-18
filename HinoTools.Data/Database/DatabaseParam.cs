namespace HinoTools.Data.Database
{
    public class DatabaseParam
    {
        public string ServerName { get; set; }

        public string UserID { get; set; }

        public string Password { get; set; }

        public string Database { get; set; }

        public int Port { get; set; } = 3306;

        public string GetConnectionStringWithoutDatabase()
        {
            return
                $"Server={ServerName};Port={Port};Uid={UserID};Pwd={Password};";
        }

        public string GetConnectionString(string database)
        {
            return
                $"Server={ServerName};Port={Port};Uid={UserID};Pwd={Password}; Database={Database}";
        }
    }
}
