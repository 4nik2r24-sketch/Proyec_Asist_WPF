using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Data;
using MySql.Data.MySqlClient;
using AplicacionMVP.Models;
using AplicacionMVP;

namespace AplicacionMVP.Vista
{
    public partial class VentanaModificarTrabajador : Window
    {
        private ConexionBD conexionBD = new ConexionBD();
        private int idTrabajador;
        public bool ModificacionExitosos { get; private set; } = false;

       
        private string origRut = "", origNombre = "", origPaterno = "", origMaterno = "", origCorreo = "", origContra = "", origRol = "", origEstado = "";
        private bool datosCargados = false;

        public VentanaModificarTrabajador(DataRowView row)
        {
            InitializeComponent();

            idTrabajador = Convert.ToInt32(row["id_usuario"]);

            origRut = txtRut.Text = row["rut"].ToString() ?? "";
            origNombre = txtNombre.Text = row["nombre"].ToString() ?? "";
            origPaterno = txtApPaterno.Text = row["apellido_paterno"].ToString() ?? "";
            origMaterno = txtApMaterno.Text = row["apellido_materno"].ToString() ?? "";
            origCorreo = txtCorreo.Text = row["correo"].ToString() ?? "";
            origContra = txtContrasena.Password = row["contrasena"].ToString() ?? "";

            cmbRol.SelectedIndex = (row["id_rol"].ToString() == "1") ? 0 : 1;
            origRol = cmbRol.SelectedIndex.ToString();

            cmbEstado.SelectedIndex = (row["estado_laboral"].ToString() == "Vigente") ? 0 : 1;
            origEstado = cmbEstado.SelectedIndex.ToString();

            datosCargados = true;
        }

        //se resalta visualmente en verde si el campo fue modificado
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!datosCargados) return;
            if (sender is TextBox txt)
            {
                string original = txt.Name switch
                {
                    "txtRut" => origRut,
                    "txtNombre" => origNombre,
                    "txtApPaterno" => origPaterno,
                    "txtApMaterno" => origMaterno,
                    "txtCorreo" => origCorreo,
                    _ => ""
                };

                if (txt.Text != original)
                {
                    txt.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Verde claro
                }
                else
                {
                    txt.ClearValue(TextBox.BackgroundProperty);
                }
            }
        }

        private void Contrasena_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!datosCargados) return;
            if (txtContrasena.Password != origContra)
            {
                txtContrasena.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
            }
            else
            {
                txtContrasena.ClearValue(PasswordBox.BackgroundProperty);
            }
        }

        private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!datosCargados) return;
            if (sender is ComboBox cmb)
            {
                string original = cmb.Name == "cmbRol" ? origRol : origEstado;
                if (cmb.SelectedIndex.ToString() != original)
                {
                    cmb.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                }
                else
                {
                    cmb.ClearValue(ComboBox.BackgroundProperty);
                }
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text;
            string apellido = txtApPaterno.Text;

            
            string detalleCambios = "";
            if (txtRut.Text != origRut) detalleCambios += $"\n- RUT: '{origRut}' ➔ '{txtRut.Text}'";
            if (txtNombre.Text != origNombre) detalleCambios += $"\n- Nombre: '{origNombre}' ➔ '{txtNombre.Text}'";
            if (txtApPaterno.Text != origPaterno) detalleCambios += $"\n- Apellido Paterno: '{origPaterno}' ➔ '{txtApPaterno.Text}'";
            if (txtApMaterno.Text != origMaterno) detalleCambios += $"\n- Apellido Materno: '{origMaterno}' ➔ '{txtApMaterno.Text}'";
            if (txtCorreo.Text != origCorreo) detalleCambios += $"\n- Correo: '{origCorreo}' ➔ '{txtCorreo.Text}'";
            if (txtContrasena.Password != origContra) detalleCambios += $"\n- Contraseña modificada";
            if (cmbRol.SelectedIndex.ToString() != origRol) detalleCambios += $"\n- Rol modificado";
            if (cmbEstado.SelectedIndex.ToString() != origEstado) detalleCambios += $"\n- Estado Laboral modificado";

            if (string.IsNullOrEmpty(detalleCambios))
            {
                Notificacion aviso = new Notificacion("No se ha detectado ningún cambio en los datos del trabajador.");
                aviso.ShowDialog();
                return;
            }

            MessageBoxResult confirmacion = MessageBox.Show(
                $"¿Está seguro de que desea modificar al trabajador {nombre} {apellido} con los siguientes cambios?{detalleCambios}",
                "Confirmar Modificación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.Yes)
            {
                int rolId = cmbRol.SelectedIndex == 0 ? 1 : 2;
                string estado = cmbEstado.SelectedIndex == 0 ? "Vigente" : "Desvinculado";

                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    try
                    {
                        con.Open();
                        string query = "UPDATE usuario SET id_rol=@rol, rut=@rut, nombre=@nombre, apellido_paterno=@paterno, " +
                                       "apellido_materno=@materno, correo=@correo, contrasena=@contra, estado_laboral=@estado " +
                                       "WHERE id_usuario=@id";

                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        {
                            cmd.Parameters.AddWithValue("@rol", rolId);
                            cmd.Parameters.AddWithValue("@rut", txtRut.Text);
                            cmd.Parameters.AddWithValue("@nombre", txtNombre.Text);
                            cmd.Parameters.AddWithValue("@paterno", txtApPaterno.Text);
                            cmd.Parameters.AddWithValue("@materno", txtApMaterno.Text);
                            cmd.Parameters.AddWithValue("@correo", txtCorreo.Text);
                            cmd.Parameters.AddWithValue("@contra", txtContrasena.Password);
                            cmd.Parameters.AddWithValue("@estado", estado);
                            cmd.Parameters.AddWithValue("@id", idTrabajador);

                            cmd.ExecuteNonQuery();

                            ModificacionExitosos = true;
                            Notificacion aviso = new Notificacion($"Los datos de {nombre} {apellido} fueron modificados exitosamente.");
                            aviso.ShowDialog();

                            this.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Notificacion aviso = new Notificacion("Error al modificar trabajador: " + ex.Message);
                        aviso.ShowDialog();
                    }
                }
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}