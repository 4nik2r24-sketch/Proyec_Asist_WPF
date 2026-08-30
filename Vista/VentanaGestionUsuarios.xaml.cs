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

                    // 1. Cargar Usuarios Vigentes (Tabla Superior)
                    string queryVigentes = @"
                        SELECT id_usuario, id_rol, rut, nombre, apellido_paterno, 
                               apellido_materno, correo, contrasena, estado_laboral
                        FROM usuario WHERE estado_laboral = 'Vigente'";

                    using (MySqlCommand cmdVigentes = new MySqlCommand(queryVigentes, con))
                    using (MySqlDataAdapter adapterVigentes = new MySqlDataAdapter(cmdVigentes))
                    {
                        DataTable dtVigentes = new DataTable();
                        adapterVigentes.Fill(dtVigentes);
                        dgUsuariosVigentes.ItemsSource = dtVigentes.DefaultView;
                    }

                    // 2. Cargar Usuarios Desvinculados (Tabla Inferior)
                    string queryDesvinculados = @"
                        SELECT id_usuario, id_rol, rut, nombre, apellido_paterno, 
                               apellido_materno, correo, contrasena, estado_laboral
                        FROM usuario WHERE estado_laboral = 'Desvinculado'";

                    using (MySqlCommand cmdDesvinculados = new MySqlCommand(queryDesvinculados, con))
                    using (MySqlDataAdapter adapterDesvinculados = new MySqlDataAdapter(cmdDesvinculados))
                    {
                        DataTable dtDesvinculados = new DataTable();
                        adapterDesvinculados.Fill(dtDesvinculados);
                        dgUsuariosDesvinculados.ItemsSource = dtDesvinculados.DefaultView;
                    }
                }
                catch (Exception ex)
                {
                    Notificacion aviso = new Notificacion("Error al cargar trabajadores: " + ex.Message);
                    aviso.ShowDialog();
                }
            }
        }

        private void TxtRut_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Formateo automático del RUT con guion antes del dígito verificador
            string text = txtRut.Text.Replace("-", "").Trim();
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
            string estado = "Vigente"; // Todo nuevo trabajador nace siendo Vigente por lógica de negocio

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

        // Botón Desvincular (Tabla Superior - Borrado lógico cambiando estado a Desvinculado)
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

        // Re-vincular al trabajador desde la tabla inferior
        private void BtnRevincular_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioSeleccionadoId == 0 || !esTablaDesvinculadosSeleccionada)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador de la lista de desvinculados para re-vincularlo.");
                aviso.ShowDialog();
                return;
            }

            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                try
                {
                    con.Open();
                    string query = "UPDATE usuario SET estado_laboral = 'Vigente' WHERE id_usuario = @id";
                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id", usuarioSeleccionadoId);
                        cmd.ExecuteNonQuery();

                        Notificacion aviso = new Notificacion("El trabajador ha sido re-vinculado exitosamente a la nómina activa.");
                        aviso.ShowDialog();

                        BtnLimpiar_Click(null, null);
                        CargarUsuarios();
                    }
                }
                catch (Exception ex)
                {
                    Notificacion err = new Notificacion("Error al re-vincular: " + ex.Message);
                    err.ShowDialog();
                }
            }
        }

        // Eliminar de forma definitiva de la base de datos (Cumple con GU-03)
        private void BtnEliminarDefinitivo_Click(object sender, RoutedEventArgs e)
        {
            if (usuarioSeleccionadoId == 0 || !esTablaDesvinculadosSeleccionada)
            {
                Notificacion aviso = new Notificacion("Debe seleccionar un trabajador de la lista de desvinculados para eliminarlo permanentemente.");
                aviso.ShowDialog();
                return;
            }

            string nombreAEliminar = txtNombre.Text;
            string apellidoAEliminar = txtApPaterno.Text;
            string rutAEliminar = txtRut.Text;

            MessageBoxResult resultado = MessageBox.Show(
                $"¿Está seguro de eliminar PERMANENTEMENTE al trabajador {nombreAEliminar} {apellidoAEliminar} (RUT: {rutAEliminar}) de la base de datos?\n\nEsta acción no se puede deshacer.",
                "Confirmar Eliminación Definitiva",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado == MessageBoxResult.Yes)
            {
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    try
                    {
                        con.Open();
                        string query = "DELETE FROM usuario WHERE id_usuario = @id";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@id", usuarioSeleccionadoId);
                            cmd.ExecuteNonQuery();

                            Notificacion aviso = new Notificacion($"El trabajador {nombreAEliminar} {apellidoAEliminar} fue eliminado permanentemente.");
                            aviso.ShowDialog();

                            BtnLimpiar_Click(null, null);
                            CargarUsuarios();
                        }
                    }
                    catch (Exception ex)
                    {
                        Notificacion aviso = new Notificacion("No se puede eliminar de forma definitiva porque el usuario tiene registros de asistencia asociados. \nError: " + ex.Message);
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
    }
}