using MySql.Data.MySqlClient;

namespace AplicacionMVP
{
    public class ConexionBD
    {
        // Cambia "tu_contrasena" por la contraseña de tu MySQL Workbench
        private string cadenaConexion = "Server=localhost; Database=sistema_asistencia; Uid=root; Pwd=;";

        public MySqlConnection ObtenerConexion()
        {
            return new MySqlConnection(cadenaConexion);
        }
    }
}