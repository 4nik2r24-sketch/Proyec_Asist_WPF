using System;
using System.Windows;
using System.IO;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;

namespace AplicacionMVP.Vista
{
    public partial class VentanaGenerarAmonestacion : Window
    {
        private int _idUsuario;
        private string _nombre;
        private string _rut;
        private string _correo;
        private int _atrasos;
        private ConexionBD conexionBD = new ConexionBD();

        public VentanaGenerarAmonestacion(int idUsuario, string nombre, string rut, string correo, int atrasos)
        {
            InitializeComponent();
            _idUsuario = idUsuario;
            _nombre = nombre;
            _rut = rut;
            _correo = correo;
            _atrasos = atrasos;

            CargarDatosCarta();
        }

        private void CargarDatosCarta()
        {
            txtEncabezado.Text = $"Funcionario: {_nombre} | RUT: {_rut} | Atrasos Registrados: {_atrasos}";
            txtFecha.Text = $"Fecha: {DateTime.Now:dd-MM-yyyy}";
            txtEstimado.Text = $"Estimado/a {_nombre},";
            txtCuerpo.Text = $"Por medio de la presente, la administración de la empresa notifica formalmente una amonestación por incurrir en reiterados atrasos injustificados (un total de {_atrasos} registros fuera del horario establecido).\n\nLe recordamos que la puntualidad es un pilar fundamental de nuestra institución. Este documento quedará registrado en su hoja de vida para los fines administrativos correspondientes.";
        }

        private void RegistrarAmonestacionEnBaseDeDatos()
        {
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                con.Open();
                string query = "INSERT INTO amonestacion (id_usuario, fecha_generacion, atrasos_acumulados) VALUES (@id, @fecha, @atrasos)";
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@id", _idUsuario);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now.Date);
                    cmd.Parameters.AddWithValue("@atrasos", _atrasos);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void BtnGenerarPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PdfDocument document = new PdfDocument();
                document.Info.Title = $"Amonestación {_nombre}";
                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);
                XTextFormatter tf = new XTextFormatter(gfx);

                
                tf.Alignment = XParagraphAlignment.Justify;

                // Definición de fuentes 
                XFont fontEmpresa = new XFont("Arial", 16, XFontStyle.Bold);
                XFont fontDepto = new XFont("Arial", 11, XFontStyle.Regular);
                XFont fontTitulo = new XFont("Arial", 14, XFontStyle.Bold);
                XFont fontCuerpo = new XFont("Arial", 11, XFontStyle.Regular);
                XFont fontNegrita = new XFont("Arial", 11, XFontStyle.Bold);
                XFont fontFirma = new XFont("Arial", 9, XFontStyle.Italic);

                // 1. Encabezado corporativo de empresa
                gfx.DrawString("EMPRESA DE PRODUCTOS QUÍMICOS", fontEmpresa, XBrushes.DarkBlue, new XRect(0, 45, page.Width, 30), XStringFormats.Center);
                gfx.DrawString("DEPARTAMENTO DE RECURSOS HUMANOS", fontDepto, XBrushes.DimGray, new XRect(0, 65, page.Width, 20), XStringFormats.Center);

 
                XPen penLinea = new XPen(XColors.DarkRed, 1.5);
                gfx.DrawLine(penLinea, 50, 95, page.Width - 50, 95);

                // 2. Título del documento
                gfx.DrawString("CARTA OFICIAL DE AMONESTACIÓN", fontTitulo, XBrushes.DarkRed, new XRect(0, 125, page.Width, 20), XStringFormats.Center);

                int margenIzq = 60;
                int anchoCaja = (int)page.Width - 120; // Márgenes 
                int posicionY = 170;

                // 3. Bloque estructurado de Datos del Trabajador
                gfx.DrawString($"Fecha de Emisión: {DateTime.Now:dd} de {DateTime.Now:MMMM} de {DateTime.Now:yyyy}", fontCuerpo, XBrushes.Black, margenIzq, posicionY);
                posicionY += 20;
                gfx.DrawString($"Funcionario: {_nombre}", fontNegrita, XBrushes.Black, margenIzq, posicionY);
                posicionY += 20;
                gfx.DrawString($"RUT: {_rut}", fontCuerpo, XBrushes.Black, margenIzq, posicionY);
                posicionY += 40;

                // 4. Saludo inicial
                gfx.DrawString("Estimado/a,", fontCuerpo, XBrushes.Black, margenIzq, posicionY);
                posicionY += 25;

                // 5. Párrafos del cuerpo
                string parrafo1 = $"Por medio del presente documento, la administración le notifica formalmente una amonestación debido a que ha incurrido en reiterados atrasos injustificados durante el mes de {DateTime.Now:MMMM}, sumando un total de {_atrasos} registros fuera del horario de ingreso establecido en su contrato.";
                tf.DrawString(parrafo1, fontCuerpo, XBrushes.Black, new XRect(margenIzq, posicionY, anchoCaja, 50));
                posicionY += 55;

                string parrafo2 = "Le recordamos enfáticamente que la puntualidad es un pilar fundamental para el correcto funcionamiento de nuestras operaciones. La reiteración de estas faltas afecta directamente la planificación corporativa y el compromiso con el equipo de trabajo.";
                tf.DrawString(parrafo2, fontCuerpo, XBrushes.Black, new XRect(margenIzq, posicionY, anchoCaja, 50));
                posicionY += 55;

                string parrafo3 = "Dejamos constancia que este documento quedará archivado de forma permanente en su hoja de vida laboral para los fines administrativos que la empresa estime convenientes.";
                tf.DrawString(parrafo3, fontCuerpo, XBrushes.Black, new XRect(margenIzq, posicionY, anchoCaja, 40));
                posicionY += 50;

                gfx.DrawString("Atentamente,", fontCuerpo, XBrushes.Black, margenIzq, posicionY);

                // 6. Bloque de firmas (Empresa y Trabajador)
                posicionY += 100;

                // Firma Izquierda (Recursos Humanos)
                gfx.DrawLine(XPens.Black, margenIzq, posicionY, margenIzq + 180, posicionY);
                gfx.DrawString("Departamento de RR.HH.", fontNegrita, XBrushes.Black, margenIzq, posicionY + 10);
                gfx.DrawString("Empresa de Productos Químicos", fontFirma, XBrushes.DimGray, margenIzq, posicionY + 25);

                // Firma Derecha (Trabajador)
                int margenFirmaDer = (int)page.Width - margenIzq - 180;
                gfx.DrawLine(XPens.Black, margenFirmaDer, posicionY, margenFirmaDer + 180, posicionY);
                gfx.DrawString("Firma Toma de Conocimiento", fontNegrita, XBrushes.Black, margenFirmaDer, posicionY + 10);
                gfx.DrawString($"RUT: {_rut}", fontFirma, XBrushes.DimGray, margenFirmaDer, posicionY + 25);

                // Crear y abrir el archivo temporal
                string filepath = Path.Combine(Path.GetTempPath(), $"Amonestacion_{_nombre.Replace(" ", "_")}.pdf");
                document.Save(filepath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = filepath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al generar PDF: " + ex.Message);
                aviso.ShowDialog();
            }
        }

        private void BtnEnviarCorreo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistrarAmonestacionEnBaseDeDatos();

                Notificacion aviso = new Notificacion($"El documento fue enviado correctamente al correo: {_correo}");
                aviso.ShowDialog();

                this.Close();
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al registrar amonestación: " + ex.Message);
                aviso.ShowDialog();
            }
        }
    }
}