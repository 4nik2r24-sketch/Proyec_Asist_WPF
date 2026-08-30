using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using AplicacionMVP.Models;
using AplicacionMVP;

namespace AplicacionMVP.Vista
{
    public partial class VentanaGestionUsuarios : Window
    {
        private ConexionBD conexionBD = new ConexionBD();
        private int usuarioSeleccionadoId = 0;
        private Usuario? usuarioLogueado;

        public VentanaGestionUsuarios()
        {
            InitializeComponent();
            CargarUsuarios();
        }

        public VentanaGestionUsuarios(Usuario? usuario) : this()
        {
            usuarioLogueado = usuario;
        }

        private void CargarUsuarios()
        {
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                try
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            u.id_usuario, u.id_rol, u.rut, u.nombre, u.apellido_paterno, 
                            u.apellido_materno, u.correo, u.contrasena, u.estado_laboral,
                            (SELECT COUNT(*) FROM asistencia a WHERE a.id_usuario = u.id_usuario AND a.hora_entrada > '09:30:00') AS cantidad_atrasos
                        FROM usuario u";

                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        dgUsuarios.ItemsSource = dt.DefaultView;
                    }
                }
                catch (Exception ex)
                {
                    Notificacion aviso = new Notificacion("Error al cargar trabajadores: " + ex.Message);
                    aviso.ShowDialog();
                }
            }
        }

        private void BtnCrear_Click(object sender, RoutedEventArgs e)
        {
            string rutIngresado = txtRut.Text.Trim();
            string correoIngresado = txtCorreo.Text.Trim();
            string nombreIngresado = txtNombre.Text.Trim();

            if (string.IsNullOrEmpty(rutIngresado) || string.IsNullOrEmpty(nombreIngresado))
            {
                Notificacion aviso = new Notificacion("Por favor, complete al menos el RUT y el Nombre del trabajador.");
                aviso.ShowDialog();
                return;
            }

            int rolId = cmbRol.SelectedIndex == 0 ? 1 : 2;
            string estado = (cmbEstado.SelectedIndex == 1) ? "Desvinculado" : "Vigente";

            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                try
                {
                    con.Open();

                    // 1. VALIDACIÓN: Verificar si ya existe un trabajador con el mismo RUT o Correo
                    string queryVerificar = "SELECT COUNT(*) FROM usuario WHERE rut = @rut OR correo = @correo";
                    using (MySqlCommand cmdCheck = new MySqlCommand(queryVerificar, con))
                    {
                        cmdCheck.Parameters.AddWithValue("@rut", rutIngresado);
                        cmdCheck.Parameters.AddWithValue("@correo", correoIngresado);

                        long existe = Convert.ToInt64(cmdCheck.ExecuteScalar());
                        if (existe > 0)
                        {
                            Notificacion avisoDuplicado = new Notificacion("Acción denegada: Ya existe un trabajador registrado con este RUT o Correo electrónico.");
                            avisoDuplicado.ShowDialog();
                            return;
                        }
                    }

                    // 2. INSERCIÓN: Proceder con la creación
                    string query = @"INSERT INTO usuario 
                                     (id_rol, rut, nombre, apellido_paterno, apellido_materno, correo, contrasena, estado_laboral) 
                                     VALUES (@rol, @rut, @nombre, @paterno, @materno, @correo, @contra, @estado)";

                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@rol", rolId);
                        cmd.Parameters.AddWithValue("@rut", rutIngresado);
                        cmd.Parameters.AddWithValue("@nombre", nombreIngresado);
                        cmd.Parameters.AddWithValue("@paterno", txtApPaterno.Text.Trim());
                        cmd.Parameters.AddWithValue("@materno", txtApMaterno.Text.Trim());
                        cmd.Parameters.AddWithValue("@correo", correoIngresado);
                        cmd.Parameters.AddWithValue("@contra", txtContrasena.Password);
                        cmd.Parameters.AddWithValue("@estado", estado);

                        cmd.ExecuteNonQuery();

                        Notificacion aviso = new Notificacion("Trabajador registrado exitosamente.");
                        aviso.ShowDialog();

                        BtnLimpiar_Click(null, null);
                        CargarUsuarios();
                    }
                }
                catch (Exception ex)
                {
                    Notificacion aviso = new Notificacion("Error al registrar trabajador: " + ex.Message);
                    aviso.ShowDialog();
                }
            }
        }

        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioSeleccionadoId == 0 || dgUsuarios.SelectedItem is not DataRowView rowSeleccionada)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador de la lista primero.");
                aviso.ShowDialog();
                return;
            }

            VentanaModificarTrabajador ventanaMod = new VentanaModificarTrabajador(rowSeleccionada);
            ventanaMod.ShowDialog();

            if (ventanaMod.ModificacionExitosos)
            {
                BtnLimpiar_Click(null, null);
                CargarUsuarios();
            }
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioSeleccionadoId == 0)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador de la lista primero.");
                aviso.ShowDialog();
                return;
            }

            string nombreAEliminar = txtNombre.Text;
            string apellidoAEliminar = txtApPaterno.Text;
            string rutAEliminar = txtRut.Text;

            MessageBoxResult resultado = MessageBox.Show(
                $"¿Está seguro de que desea dar de baja (desvincular) al trabajador {nombreAEliminar} {apellidoAEliminar} (RUT: {rutAEliminar})?",
                "Confirmar Desvinculación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado == MessageBoxResult.Yes)
            {
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    try
                    {
                        con.Open();
                        // Borrado lógico para proteger la integridad referencial con la tabla asistencia
                        string query = "UPDATE usuario SET estado_laboral = 'Desvinculado' WHERE id_usuario = @id";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@id", usuarioSeleccionadoId);
                            cmd.ExecuteNonQuery();

                            Notificacion aviso = new Notificacion($"El trabajador {nombreAEliminar} {apellidoAEliminar} ha sido marcado como Desvinculado.");
                            aviso.ShowDialog();

                            BtnLimpiar_Click(null, null);
                            CargarUsuarios();
                        }
                    }
                    catch (Exception ex)
                    {
                        Notificacion aviso = new Notificacion("Error al cambiar el estado del trabajador: " + ex.Message);
                        aviso.ShowDialog();
                    }
                }
            }
        }

        private void BtnLimpiar_Click(object? sender, RoutedEventArgs? e)
        {
            usuarioSeleccionadoId = 0;
            txtRut.Clear();
            txtNombre.Clear();
            txtApPaterno.Clear();
            txtApMaterno.Clear();
            txtCorreo.Clear();
            txtContrasena.Clear();
            cmbRol.SelectedIndex = -1;
            cmbEstado.SelectedIndex = -1;
            dgUsuarios.SelectedItem = null;
        }

        private void DgUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsuarios.SelectedItem is DataRowView row)
            {
                usuarioSeleccionadoId = Convert.ToInt32(row["id_usuario"]);
                txtRut.Text = row["rut"].ToString();
                txtNombre.Text = row["nombre"].ToString();
                txtApPaterno.Text = row["apellido_paterno"].ToString();
                txtApMaterno.Text = row["apellido_materno"].ToString();
                txtCorreo.Text = row["correo"].ToString();
                txtContrasena.Password = row["contrasena"].ToString();

                cmbRol.SelectedIndex = (row["id_rol"].ToString() == "1") ? 0 : 1;
                cmbEstado.SelectedIndex = (row["estado_laboral"].ToString() == "Vigente") ? 0 : 1;
            }
        }

        private void BtnVerReportes_Click(object sender, RoutedEventArgs e)
        {
            VentanaReportes reportes = new VentanaReportes(usuarioLogueado);
            reportes.Show();
            this.Close();
        }

        private void BtnVerHistorial_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsuarios.SelectedItem is DataRowView row)
            {
                string nombreCompleto = $"{row["nombre"]} {row["apellido_paterno"]}";
                string correoDestino = row["correo"].ToString() ?? "";
                int atrasos = Convert.ToInt32(row["cantidad_atrasos"]);

                if (atrasos >= 3)
                {
                    MessageBoxResult respuesta = MessageBox.Show(
                        $"El trabajador {nombreCompleto} tiene {atrasos} atrasos registrados.\n\n¿Desea generar y enviar la carta de amonestación en formato PDF a {correoDestino}?",
                        "Amonestación Requerida",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (respuesta == MessageBoxResult.Yes)
                    {
                        GenerarYEnviarAmonestacion(nombreCompleto, correoDestino, atrasos);
                    }
                }
                else
                {
                    Notificacion aviso = new Notificacion($"El trabajador {nombreCompleto} tiene {atrasos} atrasos. Aún no amerita amonestación (mínimo 3).");
                    aviso.ShowDialog();
                }
            }
            else
            {
                Notificacion aviso = new Notificacion("Por favor, seleccione primero un trabajador de la lista.");
                aviso.ShowDialog();
            }
        }

        private void GenerarYEnviarAmonestacion(string nombreCompleto, string correoDestino, int atrasos)
        {
            try
            {
                PdfDocument documento = new PdfDocument();
                documento.Info.Title = "Carta de Amonestación";

                PdfPage pagina = documento.AddPage();
                XGraphics grafico = XGraphics.FromPdfPage(pagina);

                XFont fuenteTitulo = new XFont("Arial", 16, XFontStyle.Bold);
                XFont fuenteTexto = new XFont("Arial", 12, XFontStyle.Regular);

                grafico.DrawString("SISTEMA DE ASISTENCIA - QUÍMICOS S.A.", fuenteTitulo, XBrushes.DarkViolet, new XRect(0, 50, pagina.Width, 50), XStringFormats.Center);
                grafico.DrawString("CARTA OFICIAL DE AMONESTACIÓN POR ATRASOS", fuenteTitulo, XBrushes.Black, new XRect(0, 80, pagina.Width, 50), XStringFormats.Center);

                string[] lineasTexto = {
                    $"Fecha de emisión: {DateTime.Now:dd/MM/yyyy}",
                    "",
                    $"Estimado/a trabajador/a: {nombreCompleto}",
                    "",
                    "Por medio del presente documento, la administración de Químicos S.A. le comunica",
                    $"formalmente que nuestro sistema ha registrado un total de {atrasos} atrasos",
                    "injustificados durante el periodo vigente.",
                    "",
                    "Le recordamos que la puntualidad y el cumplimiento de su horario laboral son",
                    "fundamentales para el correcto funcionamiento de las operaciones de la empresa.",
                    "",
                    "Dejamos constancia de esta notificación en su hoja de vida laboral para los",
                    "fines administrativos que correspondan.",
                    "",
                    "Atentamente,",
                    "",
                    "_______________________________",
                    "Departamento de Recursos Humanos",
                    "Químicos S.A."
                };

                int posicionY = 150;
                foreach (string linea in lineasTexto)
                {
                    grafico.DrawString(linea, fuenteTexto, XBrushes.Black, new XPoint(50, posicionY));
                    posicionY += 20;
                }

                string nombreArchivo = $"Amonestacion_{nombreCompleto.Replace(" ", "_")}.pdf";
                string rutaArchivoTemp = Path.Combine(Path.GetTempPath(), nombreArchivo);
                documento.Save(rutaArchivoTemp);

                // Intento de envío de correo por SMTP
                bool correoEnviado = false;
                try
                {
                    using (MailMessage correo = new MailMessage())
                    {
                        correo.From = new MailAddress("tucorreo@gmail.com");
                        correo.To.Add(correoDestino);
                        correo.Subject = "Notificación Importante: Recursos Humanos - Químicos S.A.";
                        correo.Body = $"Estimado/a {nombreCompleto},\n\nAdjunto a este correo encontrará una comunicación oficial de Recursos Humanos respecto a sus registros de asistencia recientes.\n\nSaludos cordiales,\nAdministración.";

                        using (Attachment adjunto = new Attachment(rutaArchivoTemp))
                        {
                            correo.Attachments.Add(adjunto);

                            using (SmtpClient clienteSmtp = new SmtpClient("smtp.gmail.com", 587))
                            {
                                clienteSmtp.Credentials = new NetworkCredential("tucorreo@gmail.com", "tu_contraseña_de_aplicacion");
                                clienteSmtp.EnableSsl = true;
                                clienteSmtp.Send(correo);
                                correoEnviado = true;
                            }
                        }
                    }
                }
                catch (Exception smtpEx)
                {
                    // Si falla el servidor SMTP (credenciales demo), se registra sin frenar el flujo del PDF
                    correoEnviado = false;
                }

                // Apertura del visor de PDF
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = rutaArchivoTemp,
                    UseShellExecute = true
                });

                string mensajeFinal = correoEnviado
                    ? "El documento PDF fue generado y enviado exitosamente al correo del trabajador"
                    : "El documento PDF fue generado con éxito\nSe abrirá en pantalla para su revisión (El correo no se envió por configuración de credenciales SMTP).";

                Notificacion avisoExito = new Notificacion(mensajeFinal);
                avisoExito.ShowDialog();
            }
            catch (Exception ex)
            {
                Notificacion error = new Notificacion("Error al procesar amonestación: " + ex.Message);
                error.ShowDialog();
            }
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            PanelPrincipalVentana panel = new PanelPrincipalVentana(usuarioLogueado);
            panel.Show();
            this.Close();
        }
    }
}