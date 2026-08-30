using System;
using System.Windows;
using MySql.Data.MySqlClient;
using AplicacionMVP.Models;
using AplicacionMVP;

namespace AplicacionMVP.Vista
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            lblError.Visibility = Visibility.Collapsed;
            string correo = txtCorreo.Text.Trim();
            string contrasena = txtContrasena.Password.Trim();

            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(contrasena))
            {
                lblError.Text = "Por favor, complete todos los campos.";
                lblError.Visibility = Visibility.Visible;
                return;
            }

            ConexionBD conexionBD = new ConexionBD();
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                try
                {
                    con.Open();
                    string query = "SELECT id_usuario, id_rol, rut, nombre, apellido_paterno, estado_laboral FROM usuario WHERE correo = @correo AND contrasena = @contrasena";
                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@correo", correo);
                        cmd.Parameters.AddWithValue("@contrasena", contrasena);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string estado = reader.GetString("estado_laboral");
                                if (estado == "Desvinculado")
                                {
                                    lblError.Text = "Su usuario se encuentra desvinculado.";
                                    lblError.Visibility = Visibility.Visible;
                                    return;
                                }

                                Usuario usuarioLogueado = new Usuario
                                {
                                    IdUsuario = reader.GetInt32("id_usuario"),
                                    IdRol = reader.GetInt32("id_rol"),
                                    Rut = reader.GetString("rut"),
                                    Nombre = reader.GetString("nombre"),
                                    ApellidoPaterno = reader.GetString("apellido_paterno")
                                };

                                MessageBox.Show($"¡Bienvenido/a {usuarioLogueado.Nombre} {usuarioLogueado.ApellidoPaterno}!", "Sesión Iniciada", MessageBoxButton.OK, MessageBoxImage.Information);

                                PanelPrincipalVentana panel = new PanelPrincipalVentana(usuarioLogueado);
                                panel.Show();
                                this.Close();
                            }
                            else
                            {
                                lblError.Text = "Correo o contraseña incorrectos.";
                                lblError.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error de base de datos: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}