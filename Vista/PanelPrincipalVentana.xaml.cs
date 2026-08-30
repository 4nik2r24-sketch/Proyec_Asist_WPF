using System;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using MySql.Data.MySqlClient;
using AplicacionMVP.Models;
using AplicacionMVP;

namespace AplicacionMVP.Vista
{
    public partial class PanelPrincipalVentana : Window
    {
        private Usuario? usuarioActual;

        public PanelPrincipalVentana()
        {
            InitializeComponent();
            IniciarReloj();
            ValidarRolUsuario();
        }

        public PanelPrincipalVentana(Usuario? usuario) : this()
        {
            usuarioActual = usuario;

            // Si el usuario llega nulo rescatamos un usuario vigente por defecto de la BD
            if (usuarioActual == null)
            {
                try
                {
                    ConexionBD conexionBD = new ConexionBD();
                    using (MySqlConnection con = conexionBD.ObtenerConexion())
                    {
                        con.Open();
                        string query = "SELECT id_usuario, rut, nombre, apellido_paterno, id_rol FROM usuario WHERE estado_laboral = 'Vigente' LIMIT 1";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    usuarioActual = new Usuario
                                    {
                                        IdUsuario = (int)reader.GetInt64("id_usuario"),
                                        Rut = reader.GetString("rut"),
                                        Nombre = reader.GetString("nombre"),
                                        ApellidoPaterno = reader.GetString("apellido_paterno"),
                                        IdRol = (int)reader.GetInt64("id_rol")
                                    };
                                }
                            }
                        }
                    }
                }
                catch
                {
                    
                }
            }

            MostrarDatosUsuario();
            ValidarRolUsuario();
            CargarHistorial();
        }

        private void IniciarReloj()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => lblReloj.Text = $"Hora actual: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            timer.Start();
        }

        private void MostrarDatosUsuario()
        {
            if (usuarioActual != null)
            {
                string modoText = usuarioActual.IdRol == 1 ? "[Modo Admin]" : "[Modo Usuario]";
                lblNombreUsuario.Text = $"Bienvenido/a: {usuarioActual.Nombre} {usuarioActual.ApellidoPaterno} {modoText}";
            }
            else
            {
                lblNombreUsuario.Text = "Bienvenido/a: Administrador";
            }
        }

        private void ValidarRolUsuario()
        {
            if (usuarioActual != null)
            {
                if (usuarioActual.IdRol == 1)
                {
                    btnAdministrador.Visibility = Visibility.Visible;
                    this.Title = "Panel de Asistencia - Ventana Admin";
                }
                else
                {
                    btnAdministrador.Visibility = Visibility.Collapsed;
                    this.Title = "Panel de Asistencia - Ventana de Usuario";
                }
            }
        }

        private void CargarHistorial()
        {
            if (usuarioActual == null) return;

            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    string query = @"
                        SELECT 
                            fecha AS 'Fecha', 
                            hora_entrada AS 'Hora Entrada', 
                            CASE 
                                WHEN hora_entrada > '09:30:00' THEN 'Sí (Atraso)'
                                ELSE 'No'
                            END AS '¿Atraso?',
                            hora_salida AS 'Hora Salida', 
                            CASE 
                                WHEN hora_salida IS NOT NULL AND hora_salida < '17:30:00' THEN 'Sí (Anticipada)'
                                ELSE 'No'
                            END AS '¿Salida Anticipada?',
                            estado_asistencia AS 'Estado'
                        FROM asistencia 
                        WHERE id_usuario = @id_usuario
                        ORDER BY fecha DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id_usuario", usuarioActual.IdUsuario);
                        MySqlDataAdapter adaptador = new MySqlDataAdapter(cmd);
                        DataTable tabla = new DataTable();
                        adaptador.Fill(tabla);

                        dgHistorial.ItemsSource = tabla.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar historial: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMarcarEntrada_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioActual == null) return;

            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");
                    string horaActualStr = DateTime.Now.ToString("HH:mm:ss");

                    string queryVerificar = "SELECT id_asistencia FROM asistencia WHERE id_usuario = @id_usuario AND fecha = @fecha";
                    using (MySqlCommand cmdCheck = new MySqlCommand(queryVerificar, con))
                    {
                        cmdCheck.Parameters.AddWithValue("@id_usuario", usuarioActual.IdUsuario);
                        cmdCheck.Parameters.AddWithValue("@fecha", fechaHoy);

                        if (cmdCheck.ExecuteScalar() != null)
                        {
                            MessageBox.Show("Acción denegada: Usted ya ha registrado su entrada el día de hoy. Solo se permite una entrada diaria.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    string queryInsert = "INSERT INTO asistencia (id_usuario, fecha, hora_entrada, estado_asistencia) VALUES (@id_usuario, @fecha, @hora, 'Presente')";
                    using (MySqlCommand cmdInsert = new MySqlCommand(queryInsert, con))
                    {
                        cmdInsert.Parameters.AddWithValue("@id_usuario", usuarioActual.IdUsuario);
                        cmdInsert.Parameters.AddWithValue("@fecha", fechaHoy);
                        cmdInsert.Parameters.AddWithValue("@hora", horaActualStr);
                        cmdInsert.ExecuteNonQuery();

                        TimeSpan horaEntradaTs = DateTime.Now.TimeOfDay;
                        TimeSpan horaLimite = new TimeSpan(9, 30, 0);

                        if (horaEntradaTs > horaLimite)
                        {
                            MessageBox.Show($"Entrada registrada a las {horaActualStr}.\n\nATENCIÓN: Su marca ha quedado registrada como ATRASO.", "Registro con Atraso", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"¡Entrada registrada exitosamente a las {horaActualStr}!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        CargarHistorial();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar entrada: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMarcarSalida_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioActual == null) return;

            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");
                    string horaActualStr = DateTime.Now.ToString("HH:mm:ss");

                    string queryVerificar = "SELECT hora_salida FROM asistencia WHERE id_usuario = @id_usuario AND fecha = @fecha";
                    using (MySqlCommand cmdCheck = new MySqlCommand(queryVerificar, con))
                    {
                        cmdCheck.Parameters.AddWithValue("@id_usuario", usuarioActual.IdUsuario);
                        cmdCheck.Parameters.AddWithValue("@fecha", fechaHoy);

                        using (MySqlDataReader reader = cmdCheck.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                MessageBox.Show("Acción denegada: No puede registrar salida sin haber registrado su entrada hoy.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            else if (!reader.IsDBNull(reader.GetOrdinal("hora_salida")))
                            {
                                MessageBox.Show("Acción denegada: Usted ya ha registrado su salida el día de hoy. Solo se permite una salida diaria.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }

                    string queryUpdate = "UPDATE asistencia SET hora_salida = @hora WHERE id_usuario = @id_usuario AND fecha = @fecha";
                    using (MySqlCommand cmdUpdate = new MySqlCommand(queryUpdate, con))
                    {
                        cmdUpdate.Parameters.AddWithValue("@id_usuario", usuarioActual.IdUsuario);
                        cmdUpdate.Parameters.AddWithValue("@fecha", fechaHoy);
                        cmdUpdate.Parameters.AddWithValue("@hora", horaActualStr);
                        cmdUpdate.ExecuteNonQuery();

                        TimeSpan horaSalidaTs = DateTime.Now.TimeOfDay;
                        TimeSpan horaSalidaOficial = new TimeSpan(17, 30, 0);

                        if (horaSalidaTs < horaSalidaOficial)
                        {
                            MessageBox.Show($"Salida registrada a las {horaActualStr}.\n\nATENCIÓN: Su marca ha quedado registrada como SALIDA ANTICIPADA.", "Salida Anticipada", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            MessageBox.Show($"¡Salida registrada exitosamente a las {horaActualStr}!", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        CargarHistorial();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al registrar salida: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAdministrador_Click(object sender, RoutedEventArgs e)
        {
            OverlaySeguridad.Visibility = Visibility.Visible;
            txtClaveAdmin.Clear();
            lblErrorClave.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelarSeguridad_Click(object sender, RoutedEventArgs e)
        {
            OverlaySeguridad.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmarSeguridad_Click(object sender, RoutedEventArgs e)
        {
            string claveIngresada = txtClaveAdmin.Password.Trim();

            if (claveIngresada == "admin123")
            {
                OverlaySeguridad.Visibility = Visibility.Collapsed;
                VentanaGestionUsuarios gestion = new VentanaGestionUsuarios(usuarioActual);
                gestion.ShowDialog();
            }
            else
            {
                lblErrorClave.Visibility = Visibility.Visible;
            }
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            
            MainWindow login = new MainWindow();
            login.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window != login)
                {
                    window.Close();
                }
            }
        }
    }
}