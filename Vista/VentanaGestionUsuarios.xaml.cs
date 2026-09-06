using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using AplicacionMVP.Models;
using AplicacionMVP;

namespace AplicacionMVP.Vista
{
    public partial class VentanaGestionUsuarios : Window
    {
        private ConexionBD conexionBD = new ConexionBD();
        private int usuarioSeleccionadoId = 0;
        private Usuario? usuarioLogueado;
        private bool esTablaDesvinculadosSeleccionada = false;

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

                    // Cargar Usuarios Vigentes con lógica mensual y contador anual
                    string queryVigentes = @"
                SELECT u.id_usuario, u.id_rol, u.rut, u.nombre, u.apellido_paterno, 
                       u.apellido_materno, u.correo, u.contrasena, u.estado,
                       -- Atrasos solo del mes actual
                       (SELECT COUNT(*) FROM asistencia a WHERE a.id_usuario = u.id_usuario AND a.hora_entrada > '09:30:00' AND a.estado_asistencia = 'Presente' AND MONTH(a.fecha) = MONTH(CURDATE()) AND YEAR(a.fecha) = YEAR(CURDATE())) AS atrasos,
                       -- Fecha de amonestación solo si fue enviada este mes
                       (SELECT MAX(fecha_generacion) FROM amonestacion am WHERE am.id_usuario = u.id_usuario AND MONTH(am.fecha_generacion) = MONTH(CURDATE()) AND YEAR(am.fecha_generacion) = YEAR(CURDATE())) AS fecha_amonestacion,
                       -- Cantidad total de amonestaciones en el año para la nueva columna visual
                       (SELECT COUNT(*) FROM amonestacion am2 WHERE am2.id_usuario = u.id_usuario AND YEAR(am2.fecha_generacion) = YEAR(CURDATE())) AS total_amonestaciones
                FROM usuario u WHERE u.estado = 'Vigente'";

                    using (MySqlCommand cmdVigentes = new MySqlCommand(queryVigentes, con))
                    using (MySqlDataAdapter adapterVigentes = new MySqlDataAdapter(cmdVigentes))
                    {
                        DataTable dtVigentes = new DataTable();
                        adapterVigentes.Fill(dtVigentes);

                        dtVigentes.Columns.Add("EstadoUI", typeof(string));
                        dtVigentes.Columns.Add("TextoBotonGris", typeof(string));

                        foreach (DataRow row in dtVigentes.Rows)
                        {
                            int atrasos = Convert.ToInt32(row["atrasos"]);
                            bool yaAmonestadoEsteMes = row["fecha_amonestacion"] != DBNull.Value;

                            if (yaAmonestadoEsteMes)
                            {
                                DateTime fecha = Convert.ToDateTime(row["fecha_amonestacion"]);
                                row["EstadoUI"] = "Enviada";
                                row["TextoBotonGris"] = $"✓ Enviada ({fecha:dd-MM-yyyy})";
                            }
                            else if (atrasos >= 4) // Solo se enciende si este mes tiene 4 o más atrasos
                            {
                                row["EstadoUI"] = "Amonestar";
                                row["TextoBotonGris"] = "";
                            }
                            else
                            {
                                row["EstadoUI"] = "Ninguno";
                                row["TextoBotonGris"] = "";
                            }
                        }

                        dgUsuariosVigentes.ItemsSource = dtVigentes.DefaultView;
                    }

                    //Cargar Usuarios Eliminados
                    string queryDesvinculados = @"SELECT u.id_usuario, u.id_rol, u.rut, u.nombre, u.apellido_paterno, u.apellido_materno, u.correo, u.contrasena, u.estado, (SELECT COUNT(*) FROM amonestacion am WHERE am.id_usuario = u.id_usuario) AS total_amonestaciones FROM usuario u WHERE u.estado = 'Eliminado'";

                    using (MySqlCommand cmdDesvinculados = new MySqlCommand(queryDesvinculados, con))
                    using (MySqlDataAdapter adapterDesvinculados = new MySqlDataAdapter(cmdDesvinculados))
                    {
                        DataTable dtDesvinculados = new DataTable();
                        adapterDesvinculados.Fill(dtDesvinculados);
                        dgUsuariosDesvinculados.ItemsSource = dtDesvinculados.DefaultView;
                    }
                }
                catch (Exception)
                {
                    Notificacion aviso = new Notificacion("No se puede eliminar definitivamente porque el usuario tiene registros de asistencia o amonestaciones asociadas.");
                    aviso.ShowDialog();
                }
            }
        }

        private void TxtRut_TextChanged(object sender, TextChangedEventArgs e)
        {
           
            string text = txtRut.Text.Replace("-", "").Trim();

            if (text.Length > 9)
            {
                text = text.Substring(0, 9);
            }

            if (text.Length > 1)
            {
                string cuerpo = text.Substring(0, text.Length - 1);
                string dv = text.Substring(text.Length - 1, 1);
                string rutFormateado = $"{cuerpo}-{dv}";

                if (txtRut.Text != rutFormateado)
                {
                    txtRut.Text = rutFormateado;
                    txtRut.CaretIndex = txtRut.Text.Length; 
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
            string estado = "Vigente";

            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                try
                {
                    con.Open();

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

                    string query = @"INSERT INTO usuario 
(id_rol, rut, nombre, apellido_paterno, apellido_materno, correo, contrasena, estado) 
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
            if (usuarioSeleccionadoId == 0 || dgUsuariosVigentes.SelectedItem is not DataRowView rowSeleccionada)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador activo de la nómina vigente para modificar.");
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

        private void BtnDesvincular_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioSeleccionadoId == 0 || esTablaDesvinculadosSeleccionada)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador activo de la nómina vigente.");
                aviso.ShowDialog();
                return;
            }

            string nombreAEliminar = txtNombre.Text;
            string apellidoAEliminar = txtApPaterno.Text;
            string rutAEliminar = txtRut.Text;

            MessageBoxResult resultado = MessageBox.Show(
                $"¿Está seguro de que desea eliminar al trabajador {nombreAEliminar} {apellidoAEliminar} (RUT: {rutAEliminar})?",
                "Confirme la acción",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado == MessageBoxResult.Yes)
            {
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    try
                    {
                        con.Open();
                        string query = "UPDATE usuario SET estado = 'Eliminado' WHERE id_usuario = @id";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@id", usuarioSeleccionadoId);
                            cmd.ExecuteNonQuery();

                            Notificacion aviso = new Notificacion($"El trabajador {nombreAEliminar} {apellidoAEliminar} ha sido eliminado.");
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
            esTablaDesvinculadosSeleccionada = false;
            txtRut.Clear();
            txtNombre.Clear();
            txtApPaterno.Clear();
            txtApMaterno.Clear();
            txtCorreo.Clear();
            txtContrasena.Clear();
            cmbRol.SelectedIndex = -1;
            dgUsuariosVigentes.SelectedItem = null;
            dgUsuariosDesvinculados.SelectedItem = null;
        }

        private void DgUsuariosVigentes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsuariosVigentes.SelectedItem is DataRowView row)
            {
                dgUsuariosDesvinculados.SelectedItem = null;
                esTablaDesvinculadosSeleccionada = false;

                usuarioSeleccionadoId = Convert.ToInt32(row["id_usuario"]);
                txtRut.Text = row["rut"].ToString();
                txtNombre.Text = row["nombre"].ToString();
                txtApPaterno.Text = row["apellido_paterno"].ToString();
                txtApMaterno.Text = row["apellido_materno"].ToString();
                txtCorreo.Text = row["correo"].ToString();
                txtContrasena.Password = row["contrasena"].ToString();

                cmbRol.SelectedIndex = (row["id_rol"].ToString() == "1") ? 0 : 1;
            }
        }

        private void DgUsuariosDesvinculados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUsuariosDesvinculados.SelectedItem is DataRowView row)
            {
                dgUsuariosVigentes.SelectedItem = null;
                esTablaDesvinculadosSeleccionada = true;

                usuarioSeleccionadoId = Convert.ToInt32(row["id_usuario"]);
                txtRut.Text = row["rut"].ToString();
                txtNombre.Text = row["nombre"].ToString();
                txtApPaterno.Text = row["apellido_paterno"].ToString();
                txtApMaterno.Text = row["apellido_materno"].ToString();
                txtCorreo.Text = row["correo"].ToString();
                txtContrasena.Password = row["contrasena"].ToString();

                cmbRol.SelectedIndex = (row["id_rol"].ToString() == "1") ? 0 : 1;
            }
        }

        private void BtnVerReportes_Click(object sender, RoutedEventArgs e)
        {
            VentanaReportes reportes = new VentanaReportes(usuarioLogueado);
            reportes.Show();
            this.Close();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            PanelPrincipalVentana panel = new PanelPrincipalVentana(usuarioLogueado);
            panel.Show();
            this.Close();
        }

        // ---FUNCIÓN PARA AMONESTAR ---
        private void BtnAmonestar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton && boton.DataContext is DataRowView fila)
            {
                int idUsuario = Convert.ToInt32(fila["id_usuario"]);
                string nombreCompleto = $"{fila["nombre"]} {fila["apellido_paterno"]}";
                string rut = fila["rut"].ToString();
                string correo = fila["correo"].ToString();
                int atrasos = Convert.ToInt32(fila["atrasos"]);

                VentanaGenerarAmonestacion ventanaCarta = new VentanaGenerarAmonestacion(idUsuario, nombreCompleto, rut, correo, atrasos);
                ventanaCarta.ShowDialog();

                CargarUsuarios(); // Recarga la tabla para que el botón se ponga gris
            }
        }
    }
}