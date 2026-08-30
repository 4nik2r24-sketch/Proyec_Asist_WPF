using System.Windows;

namespace AplicacionMVP.Vista
{
    public partial class Notificacion : Window
    {
        public Notificacion(string mensaje)
        {
            InitializeComponent();
            txtMensaje.Text = mensaje;
        }

        private void BtnAceptar_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Cierra el mensaje al hacer clic
        }
    }
}