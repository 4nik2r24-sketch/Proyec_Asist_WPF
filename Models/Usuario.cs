namespace AplicacionMVP.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }
        public string Rut { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string ApellidoPaterno { get; set; } = "";
        public string ApellidoMaterno { get; set; } = "";
        public string Correo { get; set; } = "";
        public string Contrasena { get; set; } = "";
        public string EstadoLaboral { get; set; } = "";
        public string Cargo { get; set; } = "";
    }
} 