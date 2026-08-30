using System;

namespace AplicacionMVP.Models
{
    public class Asistencia
    {
        public int IdAsistencia { get; set; }
        public int IdUsuario { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan? HoraEntrada { get; set; }
        public TimeSpan? HoraSalida { get; set; }
        public string EstadoAsistencia { get; set; } = "";
    }
}