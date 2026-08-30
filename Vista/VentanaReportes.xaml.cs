using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using AplicacionMVP.Models;
using ClosedXML.Excel;

namespace AplicacionMVP.Vista
{
    public partial class VentanaReportes : Window
    {
        private Usuario? usuarioLogueado;
        private string tipoReporteSeleccionado = "Atrasos";

        public VentanaReportes()
        {
            InitializeComponent();
            if (dpReporteDiario != null)
            {
                dpReporteDiario.SelectedDate = DateTime.Today;
                ActualizarEstadisticasDiarias(DateTime.Today);
            }
        }

        public VentanaReportes(Usuario? usuario) : this()
        {
            usuarioLogueado = usuario;

            if (usuarioLogueado == null)
            {
                try
                {
                    ConexionBD conexionBD = new ConexionBD();
                    using (MySqlConnection con = conexionBD.ObtenerConexion())
                    {
                        con.Open();
                        string query = "SELECT id_usuario, rut, nombre, apellido_paterno, id_rol FROM usuario WHERE id_rol = 1 AND estado_laboral = 'Vigente' LIMIT 1";
                        using (MySqlCommand cmd = new MySqlCommand(query, con))
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                usuarioLogueado = new Usuario
                                {
                                    IdUsuario = Convert.ToInt32(reader["id_usuario"]),
                                    Rut = reader["rut"].ToString() ?? "",
                                    Nombre = reader["nombre"].ToString() ?? "",
                                    ApellidoPaterno = reader["apellido_paterno"].ToString() ?? "",
                                    IdRol = Convert.ToInt32(reader["id_rol"])
                                };
                            }
                        }
                    }
                }
                catch
                {
                    // Manejo silencioso en caso de inicialización sin conexión previa
                }
            }
        }

        private void DpReporteDiario_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpReporteDiario.SelectedDate != null)
            {
                ActualizarEstadisticasDiarias(dpReporteDiario.SelectedDate.Value);
            }
        }

        private void ActualizarEstadisticasDiarias(DateTime fecha)
        {
            try
            {
                string fechaSql = fecha.ToString("yyyy-MM-dd");
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    string query = @"
                        SELECT u.id_usuario, a.hora_entrada, a.hora_salida, a.estado_asistencia
                        FROM usuario u 
                        LEFT JOIN asistencia a ON u.id_usuario = a.id_usuario AND a.fecha = @fecha
                        WHERE u.estado_laboral = 'Vigente'";

                    DataTable dt = new DataTable();
                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@fecha", fechaSql);
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd)) { adapter.Fill(dt); }
                    }

                    int countAsistentes = 0;
                    int countAtrasos = 0;
                    int countInasistentes = 0;
                    int countAnticipadas = 0;

                    foreach (DataRow row in dt.Rows)
                    {
                        bool tieneEntrada = row["hora_entrada"] != DBNull.Value;
                        string estadoDb = row["estado_asistencia"]?.ToString() ?? "";

                        if (!tieneEntrada || estadoDb == "Inasistente")
                        {
                            countInasistentes++;
                        }
                        else
                        {
                            countAsistentes++;
                            if (TimeSpan.TryParse(row["hora_entrada"].ToString(), out TimeSpan hEnt) && hEnt > new TimeSpan(9, 30, 0))
                            {
                                countAtrasos++;
                            }
                        }

                        if (row["hora_salida"] != DBNull.Value && TimeSpan.TryParse(row["hora_salida"].ToString(), out TimeSpan hSal) && hSal < new TimeSpan(17, 30, 0))
                        {
                            countAnticipadas++;
                        }
                    }

                    txtStatAsistentes.Text = countAsistentes.ToString();
                    txtStatAtrasos.Text = countAtrasos.ToString();
                    txtStatInasistentes.Text = countInasistentes.ToString();
                    txtStatAnticipadas.Text = countAnticipadas.ToString();
                }
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al actualizar estadísticas: " + ex.Message);
                aviso.ShowDialog();
            }
        }

        private void CargarReporte(string query, int mes, int anio, string nombreUltimaColumna)
        {
            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@mes", mes);
                        cmd.Parameters.AddWithValue("@anio", anio);

                        using (MySqlDataAdapter adaptador = new MySqlDataAdapter(cmd))
                        {
                            DataTable tabla = new DataTable();
                            adaptador.Fill(tabla);
                            dgReportes.ItemsSource = tabla.DefaultView;

                            if (colDetalle != null)
                            {
                                colDetalle.Header = nombreUltimaColumna;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al cargar el reporte: " + ex.Message);
                aviso.ShowDialog();
            }
        }

        private bool ValidarSeleccionFecha(out int mesInt, out int anioInt)
        {
            mesInt = 0;
            anioInt = 0;

            if (cmbMes.SelectedItem == null || cmbAnio.SelectedItem == null)
            {
                Notificacion aviso = new Notificacion("Por favor, selecciona un Mes y un Año para generar el reporte.");
                aviso.ShowDialog();
                return false;
            }

            string mesStr = ((ComboBoxItem)cmbMes.SelectedItem).Tag?.ToString() ?? "1";
            string anioStr = ((ComboBoxItem)cmbAnio.SelectedItem).Content?.ToString() ?? "2026";

            if (!int.TryParse(mesStr, out mesInt) || !int.TryParse(anioStr, out anioInt))
            {
                Notificacion aviso = new Notificacion("Selección de fecha inválida.");
                aviso.ShowDialog();
                return false;
            }

            return true;
        }

        private void BtnReporteDiarioExcel_Click(object sender, RoutedEventArgs e)
        {
            if (dpReporteDiario.SelectedDate == null)
            {
                Notificacion aviso = new Notificacion("Por favor, selecciona una fecha válida para el reporte diario.");
                aviso.ShowDialog();
                return;
            }

            DateTime fecha = dpReporteDiario.SelectedDate.Value;
            ActualizarEstadisticasDiarias(fecha);
            string archivoTemporal = Path.Combine(Path.GetTempPath(), $"Reporte_Diario_{fecha:yyyy-MM-dd}.xlsx");

            try
            {
                GenerarExcelDiarioFiltrado(archivoTemporal, fecha);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(archivoTemporal) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Notificacion error = new Notificacion("Error al abrir Excel diario (cierra el archivo si lo tienes abierto): " + ex.Message);
                error.ShowDialog();
            }
        }

        private void BtnAtrasos_Click(object sender, RoutedEventArgs e)
        {
            tipoReporteSeleccionado = "Atrasos";
            if (!ValidarSeleccionFecha(out int mes, out int anio)) return;

            string query = @"SELECT u.nombre AS Nombre, u.apellido_paterno AS Apellido, u.rut AS Rut, 
                                    r.nombre_rol AS Cargo, a.fecha AS Fecha, a.hora_entrada AS HoraEntrada 
                             FROM asistencia a 
                             INNER JOIN usuario u ON a.id_usuario = u.id_usuario 
                             INNER JOIN rol r ON u.id_rol = r.id_rol 
                             WHERE a.hora_entrada > '09:30:00' AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio
                             ORDER BY a.fecha DESC, a.hora_entrada DESC";

            CargarReporte(query, mes, anio, "Hora Entrada");
        }

        private void BtnSalidas_Click(object sender, RoutedEventArgs e)
        {
            tipoReporteSeleccionado = "Salidas";
            if (!ValidarSeleccionFecha(out int mes, out int anio)) return;

            string query = @"SELECT u.nombre AS Nombre, u.apellido_paterno AS Apellido, u.rut AS Rut, 
                                    r.nombre_rol AS Cargo, a.fecha AS Fecha, a.hora_salida AS HoraSalida 
                             FROM asistencia a 
                             INNER JOIN usuario u ON a.id_usuario = u.id_usuario 
                             INNER JOIN rol r ON u.id_rol = r.id_rol 
                             WHERE a.hora_salida < '17:30:00' AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio
                             ORDER BY a.fecha DESC, a.hora_salida DESC";

            CargarReporte(query, mes, anio, "Hora Salida");
        }

        private void BtnInasistencias_Click(object sender, RoutedEventArgs e)
        {
            tipoReporteSeleccionado = "Inasistencias";
            if (!ValidarSeleccionFecha(out int mesInt, out int anioInt)) return;

            int totalDiasMes = DateTime.DaysInMonth(anioInt, mesInt);
            int diasHastaHoy = (anioInt == DateTime.Today.Year && mesInt == DateTime.Today.Month) ? DateTime.Today.Day : totalDiasMes;
            if (diasHastaHoy > totalDiasMes) diasHastaHoy = totalDiasMes;

            DataTable dtResultado = new DataTable();
            dtResultado.Columns.Add("Nombre", typeof(string));
            dtResultado.Columns.Add("Apellido", typeof(string));
            dtResultado.Columns.Add("Rut", typeof(string));
            dtResultado.Columns.Add("Cargo", typeof(string));
            dtResultado.Columns.Add("Fecha", typeof(string));
            dtResultado.Columns.Add("HoraEntrada", typeof(string));

            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    string queryUsuarios = @"
                        SELECT u.id_usuario, u.rut, u.nombre, u.apellido_paterno, r.nombre_rol AS cargo 
                        FROM usuario u 
                        INNER JOIN rol r ON u.id_rol = r.id_rol 
                        WHERE u.estado_laboral = 'Vigente'";

                    DataTable dtUsuarios = new DataTable();
                    using (MySqlDataAdapter adapterU = new MySqlDataAdapter(queryUsuarios, con)) { adapterU.Fill(dtUsuarios); }

                    string queryAsistencias = @"
                        SELECT id_usuario, DAY(fecha) AS dia, fecha, hora_entrada, estado_asistencia 
                        FROM asistencia 
                        WHERE MONTH(fecha) = @mes AND YEAR(fecha) = @anio";

                    DataTable dtAsistencias = new DataTable();
                    using (MySqlCommand cmdA = new MySqlCommand(queryAsistencias, con))
                    {
                        cmdA.Parameters.AddWithValue("@mes", mesInt);
                        cmdA.Parameters.AddWithValue("@anio", anioInt);
                        using (MySqlDataAdapter adapterA = new MySqlDataAdapter(cmdA)) { adapterA.Fill(dtAsistencias); }
                    }

                    foreach (DataRow uRow in dtUsuarios.Rows)
                    {
                        long idUsr = Convert.ToInt64(uRow["id_usuario"]);
                        string nombre = uRow["nombre"].ToString() ?? "";
                        string apellido = uRow["apellido_paterno"].ToString() ?? "";
                        string rut = uRow["rut"].ToString() ?? "";
                        string cargo = uRow["cargo"].ToString() ?? "";

                        for (int d = diasHastaHoy; d >= 1; d--)
                        {
                            DateTime dtCheck = new DateTime(anioInt, mesInt, d);
                            if (dtCheck.DayOfWeek == DayOfWeek.Saturday || dtCheck.DayOfWeek == DayOfWeek.Sunday) continue;

                            DataRow[] rows = dtAsistencias.Select($"id_usuario = {idUsr} AND dia = {d}");

                            // Si no hay registro o está marcado como Inasistente sin hora de entrada
                            bool esInasistente = rows.Length == 0 ||
                                                 rows[0]["estado_asistencia"]?.ToString() == "Inasistente" ||
                                                 rows[0]["hora_entrada"] == DBNull.Value;

                            if (esInasistente)
                            {
                                dtResultado.Rows.Add(nombre, apellido, rut, cargo, dtCheck.ToString("dd/MM/yyyy"), "Inasistente");
                            }
                        }
                    }
                }
                dgReportes.ItemsSource = dtResultado.DefaultView;
                if (colDetalle != null) colDetalle.Header = "Estado";
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al cargar inasistencias: " + ex.Message);
                aviso.ShowDialog();
            }
        }

        private void BtnInformeGeneralVistaPrevia_Click(object sender, RoutedEventArgs e)
        {
            tipoReporteSeleccionado = "InformeGeneral";
            if (!ValidarSeleccionFecha(out _, out _)) return;

            string query = @"SELECT u.nombre AS Nombre, u.apellido_paterno AS Apellido, u.rut AS Rut, 
                                    r.nombre_rol AS Cargo, 'Matriz Mensual' AS Fecha, 'Vista Previa Activa' AS HoraEntrada 
                             FROM usuario u 
                             INNER JOIN rol r ON u.id_rol = r.id_rol 
                             WHERE u.estado_laboral = 'Vigente' 
                             ORDER BY u.id_usuario ASC";

            try
            {
                ConexionBD conexionBD = new ConexionBD();
                using (MySqlConnection con = conexionBD.ObtenerConexion())
                {
                    con.Open();
                    using (MySqlDataAdapter adaptador = new MySqlDataAdapter(query, con))
                    {
                        DataTable tabla = new DataTable();
                        adaptador.Fill(tabla);
                        dgReportes.ItemsSource = tabla.DefaultView;

                        if (colDetalle != null) colDetalle.Header = "Detalle";
                    }
                }
            }
            catch (Exception ex)
            {
                Notificacion aviso = new Notificacion("Error al cargar el reporte general: " + ex.Message);
                aviso.ShowDialog();
            }
        }

        private void BtnExportarActivo_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidarSeleccionFecha(out int mesInt, out int anioInt)) return;

            string mesTexto = ((ComboBoxItem)cmbMes.SelectedItem).Content.ToString() ?? "Mes";
            string anioStr = anioInt.ToString();
            string archivoTemporal = Path.Combine(Path.GetTempPath(), $"Reporte_{tipoReporteSeleccionado}_{mesTexto}_{anioStr}.xlsx");

            try
            {
                if (tipoReporteSeleccionado == "InformeGeneral")
                {
                    GenerarExcelInformeGeneralConDisenoCompleto(archivoTemporal, mesInt, anioInt, mesTexto, anioStr);
                }
                else if (tipoReporteSeleccionado == "Inasistencias")
                {
                    GenerarExcelInasistenciasConDiseno(archivoTemporal, mesInt, anioInt, mesTexto, anioStr);
                }
                else if (tipoReporteSeleccionado == "Atrasos")
                {
                    GenerarReporteConDobleTablaCompleto(
                        "REPORTE DETALLADO DE ATRASOS", archivoTemporal,
                        @"SELECT u.id_usuario, u.nombre, u.apellido_paterno, u.rut, r.nombre_rol AS Cargo, a.fecha AS Fecha, a.hora_entrada AS HoraEntrada 
                          FROM asistencia a INNER JOIN usuario u ON a.id_usuario = u.id_usuario INNER JOIN rol r ON u.id_rol = r.id_rol 
                          WHERE a.hora_entrada > '09:30:00' AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio 
                          ORDER BY a.fecha DESC, a.hora_entrada DESC",
                        "Fecha Atraso", "Hora Entrada",
                        @"SELECT u.id_usuario, 
                                (SELECT COUNT(*) FROM asistencia a WHERE a.id_usuario = u.id_usuario AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio AND a.hora_entrada > '09:30:00') AS total_valor 
                          FROM usuario u WHERE u.estado_laboral = 'Vigente'",
                        mesInt, anioInt, mesTexto, anioStr
                    );
                }
                else if (tipoReporteSeleccionado == "Salidas")
                {
                    GenerarReporteConDobleTablaCompleto(
                        "REPORTE DETALLADO DE SALIDAS ANTICIPADAS", archivoTemporal,
                        @"SELECT u.id_usuario, u.nombre, u.apellido_paterno, u.rut, r.nombre_rol AS Cargo, a.fecha AS Fecha, a.hora_salida AS HoraSalida 
                          FROM asistencia a INNER JOIN usuario u ON a.id_usuario = u.id_usuario INNER JOIN rol r ON u.id_rol = r.id_rol 
                          WHERE a.hora_salida < '17:30:00' AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio 
                          ORDER BY a.fecha DESC, a.hora_salida DESC",
                        "Fecha Salida", "Hora Salida",
                        @"SELECT u.id_usuario, 
                                (SELECT COUNT(*) FROM asistencia a WHERE a.id_usuario = u.id_usuario AND MONTH(a.fecha) = @mes AND YEAR(a.fecha) = @anio AND a.hora_salida < '17:30:00') AS total_valor 
                          FROM usuario u WHERE u.estado_laboral = 'Vigente'",
                        mesInt, anioInt, mesTexto, anioStr
                    );
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(archivoTemporal) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Notificacion error = new Notificacion("Error al abrir Excel (cierra el archivo si lo tienes abierto): " + ex.Message);
                error.ShowDialog();
            }
        }

        private void GenerarExcelDiarioFiltrado(string rutaArchivo, DateTime fecha)
        {
            ConexionBD conexionBD = new ConexionBD();
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                con.Open();
                string queryDatos = @"
                    SELECT u.id_usuario, u.rut, u.nombre, u.apellido_paterno, r.nombre_rol AS cargo, 
                           a.hora_entrada, a.hora_salida, a.estado_asistencia
                    FROM usuario u 
                    INNER JOIN rol r ON u.id_rol = r.id_rol 
                    LEFT JOIN asistencia a ON u.id_usuario = a.id_usuario AND a.fecha = @fecha
                    WHERE u.estado_laboral = 'Vigente'
                    ORDER BY a.hora_entrada DESC, u.id_usuario ASC";

                DataTable dtDatos = new DataTable();
                using (MySqlCommand cmd = new MySqlCommand(queryDatos, con))
                {
                    cmd.Parameters.AddWithValue("@fecha", fecha.ToString("yyyy-MM-dd"));
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd)) { adapter.Fill(dtDatos); }
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Reporte_Diario");
                    ws.TabColor = XLColor.FromArgb(20, 60, 120);
                    ws.ShowGridLines = true;

                    ws.Range("B2:I3").Merge().Value = "REPORTE DIARIO DE ASISTENCIA";
                    ws.Range("B2:I3").Style.Fill.BackgroundColor = XLColor.FromArgb(20, 50, 90);
                    ws.Range("B2:I3").Style.Font.FontColor = XLColor.White;
                    ws.Range("B2:I3").Style.Font.Bold = true;
                    ws.Range("B2:I3").Style.Font.FontSize = 16;
                    ws.Range("B2:I3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("B2:I3").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Range("B4:I4").Merge().Value = $"Fecha de Emisión: {fecha.ToString("dd 'DE' MMMM 'DE' yyyy").ToUpper()}";
                    ws.Range("B4:I4").Style.Font.Bold = true;
                    ws.Range("B4:I4").Style.Font.FontSize = 10;
                    ws.Range("B4:I4").Style.Font.FontColor = XLColor.FromArgb(108, 117, 125);
                    ws.Range("B4:I4").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("B4:I4").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Cell(6, 2).Value = "1. REGISTRO GENERAL DE ASISTENCIA";
                    ws.Range(6, 2, 6, 9).Merge().Style.Font.Bold = true;
                    ws.Range(6, 2, 6, 9).Style.Font.FontColor = XLColor.FromArgb(25, 60, 110);

                    int fHead1 = 7;
                    ws.Cell(fHead1, 2).Value = "ID";
                    ws.Cell(fHead1, 3).Value = "RUT";
                    ws.Cell(fHead1, 4).Value = "Nombre Completo";
                    ws.Cell(fHead1, 5).Value = "Cargo";
                    ws.Cell(fHead1, 6).Value = "Entrada";
                    ws.Cell(fHead1, 7).Value = "Salida";
                    ws.Cell(fHead1, 8).Value = "Estado";
                    ws.Cell(fHead1, 9).Value = "Observación";

                    var rHead1 = ws.Range(fHead1, 2, fHead1, 9);
                    rHead1.Style.Fill.BackgroundColor = XLColor.FromArgb(25, 60, 110);
                    rHead1.Style.Font.FontColor = XLColor.White;
                    rHead1.Style.Font.Bold = true;
                    rHead1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    int fData1 = 8;
                    foreach (DataRow row in dtDatos.Rows)
                    {
                        int idUsuario = row["id_usuario"] != DBNull.Value ? Convert.ToInt32(row["id_usuario"]) : 0;
                        string rut = row["rut"] != DBNull.Value ? row["rut"].ToString() ?? "-" : "-";
                        string nombreCompleto = $"{row["nombre"]} {row["apellido_paterno"]}";
                        string cargo = row["cargo"].ToString() ?? "";

                        string entradaStr = row["hora_entrada"] != DBNull.Value ? row["hora_entrada"].ToString()! : "-";
                        string salidaStr = row["hora_salida"] != DBNull.Value ? row["hora_salida"].ToString()! : "-";
                        string estadoFinal = (row["hora_entrada"] == DBNull.Value || row["estado_asistencia"]?.ToString() == "Inasistente") ? "Inasistente" : "Presente";
                        string observacion = "Normal";

                        if (estadoFinal == "Inasistente")
                        {
                            observacion = "Sin registro";
                            entradaStr = "-";
                            salidaStr = "-";
                        }
                        else
                        {
                            bool esAtrasado = TimeSpan.TryParse(entradaStr, out TimeSpan hEnt) && hEnt > new TimeSpan(9, 30, 0);
                            bool esAnticipada = row["hora_salida"] != DBNull.Value && TimeSpan.TryParse(salidaStr, out TimeSpan hSal) && hSal < new TimeSpan(17, 30, 0);

                            if (esAtrasado && esAnticipada) observacion = "Atraso y Salida Anticipada";
                            else if (esAtrasado) observacion = "Atraso";
                            else if (esAnticipada) observacion = "Salida Anticipada";
                        }

                        ws.Cell(fData1, 2).Value = idUsuario;
                        ws.Cell(fData1, 3).Value = rut;
                        ws.Cell(fData1, 4).Value = nombreCompleto;
                        ws.Cell(fData1, 5).Value = cargo;
                        ws.Cell(fData1, 6).Value = entradaStr;
                        ws.Cell(fData1, 7).Value = salidaStr;
                        ws.Cell(fData1, 8).Value = estadoFinal;
                        ws.Cell(fData1, 9).Value = observacion;

                        for (int c = 2; c <= 9; c++)
                        {
                            ws.Cell(fData1, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            ws.Cell(fData1, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(fData1, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }
                        fData1++;
                    }

                    int filaActual = fData1 + 2;
                    ws.Cell(filaActual, 2).Value = "2. CONTROL DE PUNTUALIDAD Y ATRASOS (HORA LÍMITE: 09:30 AM)";
                    ws.Range(filaActual, 2, filaActual, 7).Merge().Style.Font.Bold = true;
                    ws.Range(filaActual, 2, filaActual, 7).Style.Font.FontColor = XLColor.FromArgb(160, 110, 0);
                    filaActual++;

                    int fHead2 = filaActual;
                    ws.Cell(fHead2, 2).Value = "ID";
                    ws.Cell(fHead2, 3).Value = "RUT";
                    ws.Cell(fHead2, 4).Value = "Nombre Completo";
                    ws.Cell(fHead2, 5).Value = "Cargo";
                    ws.Cell(fHead2, 6).Value = "Hora Entrada";
                    ws.Cell(fHead2, 7).Value = "Detalle Atraso";

                    var rHead2 = ws.Range(fHead2, 2, fHead2, 7);
                    rHead2.Style.Fill.BackgroundColor = XLColor.FromArgb(183, 149, 11);
                    rHead2.Style.Font.FontColor = XLColor.White;
                    rHead2.Style.Font.Bold = true;
                    rHead2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    filaActual++;

                    foreach (DataRow row in dtDatos.Rows)
                    {
                        if (row["hora_entrada"] != DBNull.Value && TimeSpan.TryParse(row["hora_entrada"].ToString(), out TimeSpan hEnt) && hEnt > new TimeSpan(9, 30, 0))
                        {
                            TimeSpan diferencia = hEnt - new TimeSpan(9, 30, 0);
                            string detalleAtraso = diferencia.TotalHours >= 1
                                ? $"Atraso (+{(int)diferencia.TotalHours}h {diferencia.Minutes}min)"
                                : $"Atraso (+{(int)diferencia.TotalMinutes} min)";

                            ws.Cell(filaActual, 2).Value = Convert.ToInt32(row["id_usuario"]);
                            ws.Cell(filaActual, 3).Value = row["rut"]?.ToString() ?? "-";
                            ws.Cell(filaActual, 4).Value = $"{row["nombre"]} {row["apellido_paterno"]}";
                            ws.Cell(filaActual, 5).Value = row["cargo"]?.ToString() ?? "";
                            ws.Cell(filaActual, 6).Value = hEnt.ToString(@"hh\:mm\:ss");
                            ws.Cell(filaActual, 7).Value = detalleAtraso;

                            for (int c = 2; c <= 7; c++)
                            {
                                ws.Cell(filaActual, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Cell(filaActual, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                ws.Cell(filaActual, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                            }
                            filaActual++;
                        }
                    }

                    filaActual += 2;
                    ws.Cell(filaActual, 2).Value = "3. CONTROL DE SALIDAS ANTICIPADAS (HORARIO DE SALIDA: 17:30 PM)";
                    ws.Range(filaActual, 2, filaActual, 7).Merge().Style.Font.Bold = true;
                    ws.Range(filaActual, 2, filaActual, 7).Style.Font.FontColor = XLColor.FromArgb(146, 43, 33);
                    filaActual++;

                    int fHead3 = filaActual;
                    ws.Cell(fHead3, 2).Value = "ID";
                    ws.Cell(fHead3, 3).Value = "RUT";
                    ws.Cell(fHead3, 4).Value = "Nombre Completo";
                    ws.Cell(fHead3, 5).Value = "Cargo";
                    ws.Cell(fHead3, 6).Value = "Hora Salida";
                    ws.Cell(fHead3, 7).Value = "Detalle Salida";

                    var rHead3 = ws.Range(fHead3, 2, fHead3, 7);
                    rHead3.Style.Fill.BackgroundColor = XLColor.FromArgb(146, 43, 33);
                    rHead3.Style.Font.FontColor = XLColor.White;
                    rHead3.Style.Font.Bold = true;
                    rHead3.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    filaActual++;

                    foreach (DataRow row in dtDatos.Rows)
                    {
                        if (row["hora_salida"] != DBNull.Value && TimeSpan.TryParse(row["hora_salida"].ToString(), out TimeSpan hSal) && hSal < new TimeSpan(17, 30, 0))
                        {
                            int minutos = (int)(new TimeSpan(17, 30, 0) - hSal).TotalMinutes;
                            ws.Cell(filaActual, 2).Value = Convert.ToInt32(row["id_usuario"]);
                            ws.Cell(filaActual, 3).Value = row["rut"]?.ToString() ?? "-";
                            ws.Cell(filaActual, 4).Value = $"{row["nombre"]} {row["apellido_paterno"]}";
                            ws.Cell(filaActual, 5).Value = row["cargo"]?.ToString() ?? "";
                            ws.Cell(filaActual, 6).Value = hSal.ToString(@"hh\:mm\:ss");
                            ws.Cell(filaActual, 7).Value = $"Salida Anticipada (-{minutos} min)";

                            for (int c = 2; c <= 7; c++)
                            {
                                ws.Cell(filaActual, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Cell(filaActual, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                ws.Cell(filaActual, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                            }
                            filaActual++;
                        }
                    }

                    int fFirma = filaActual + 3;
                    ws.Range(fFirma, 3, fFirma, 8).Merge().Value = "__________________________________________________";
                    ws.Range(fFirma, 3, fFirma, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma, 3, fFirma, 8).Style.Font.Bold = true;

                    ws.Range(fFirma + 1, 3, fFirma + 1, 8).Merge().Value = "DIRECTOR / JEFE DE RECURSOS HUMANOS";
                    ws.Range(fFirma + 1, 3, fFirma + 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 8).Style.Font.Bold = true;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 8).Style.Font.FontSize = 9;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 8).Style.Font.FontColor = XLColor.DimGray;

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(rutaArchivo);
                }
            }
        }

        private void GenerarExcelInformeGeneralConDisenoCompleto(string rutaArchivo, int mesInt, int anioInt, string mesTexto, string anioStr)
        {
            ConexionBD conexionBD = new ConexionBD();
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                con.Open();
                string queryUsuarios = "SELECT u.id_usuario, u.rut, u.nombre, u.apellido_paterno, r.nombre_rol AS cargo FROM usuario u INNER JOIN rol r ON u.id_rol = r.id_rol WHERE u.estado_laboral = 'Vigente'";
                DataTable dtUsuarios = new DataTable();
                using (MySqlDataAdapter adapterU = new MySqlDataAdapter(queryUsuarios, con)) { adapterU.Fill(dtUsuarios); }

                int totalEmpleados = dtUsuarios.Rows.Count;
                int totalDiasMes = DateTime.DaysInMonth(anioInt, mesInt);

                string queryAsistencias = "SELECT id_usuario, DAY(fecha) AS dia, fecha, hora_entrada, estado_asistencia FROM asistencia WHERE MONTH(fecha) = @mes AND YEAR(fecha) = @anio";
                DataTable dtAsistencias = new DataTable();
                using (MySqlCommand cmdA = new MySqlCommand(queryAsistencias, con))
                {
                    cmdA.Parameters.AddWithValue("@mes", mesInt);
                    cmdA.Parameters.AddWithValue("@anio", anioInt);
                    using (MySqlDataAdapter adapterA = new MySqlDataAdapter(cmdA)) { adapterA.Fill(dtAsistencias); }
                }

                int totalAsistenciasReal = 0;
                int totalDiasHabiblesEvaluados = 0;
                int diasHastaHoy = (anioInt == DateTime.Today.Year && mesInt == DateTime.Today.Month) ? DateTime.Today.Day : totalDiasMes;
                if (diasHastaHoy > totalDiasMes) diasHastaHoy = totalDiasMes;

                for (int d = 1; d <= diasHastaHoy; d++)
                {
                    DateTime dtCheck = new DateTime(anioInt, mesInt, d);
                    if (dtCheck.DayOfWeek != DayOfWeek.Saturday && dtCheck.DayOfWeek != DayOfWeek.Sunday) totalDiasHabiblesEvaluados++;
                }

                int totalEsperadoAsistencias = totalEmpleados * totalDiasHabiblesEvaluados;
                foreach (DataRow userRow in dtUsuarios.Rows)
                {
                    long idUsr = Convert.ToInt64(userRow["id_usuario"]);
                    for (int d = 1; d <= diasHastaHoy; d++)
                    {
                        DateTime dtCheck = new DateTime(anioInt, mesInt, d);
                        if (dtCheck.DayOfWeek == DayOfWeek.Saturday || dtCheck.DayOfWeek == DayOfWeek.Sunday) continue;
                        DataRow[] rows = dtAsistencias.Select($"id_usuario = {idUsr} AND dia = {d}");
                        if (rows.Length > 0 && rows[0]["hora_entrada"] != DBNull.Value && rows[0]["estado_asistencia"]?.ToString() != "Inasistente")
                        {
                            totalAsistenciasReal++;
                        }
                    }
                }
                double porcentajeAsistencia = (totalEsperadoAsistencias > 0) ? Math.Round(((double)totalAsistenciasReal / totalEsperadoAsistencias) * 100, 1) : 0.0;

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Informe_General");
                    ws.TabColor = XLColor.FromArgb(20, 60, 120);
                    ws.ShowGridLines = true;

                    ws.Range("F2:AI2").Merge().Value = "INFORME GENERAL DE ASISTENCIA Y ATRASOS";
                    ws.Range("F2:AI2").Style.Font.Bold = true;
                    ws.Range("F2:AI2").Style.Font.FontSize = 18;
                    ws.Range("F2:AI2").Style.Font.FontColor = XLColor.FromArgb(20, 50, 100);
                    ws.Range("F2:AI2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("F3:AI3").Merge().Value = $"{mesTexto.ToUpper()} {anioStr}";
                    ws.Range("F3:AI3").Style.Font.Bold = true;
                    ws.Range("F3:AI3").Style.Font.FontSize = 12;
                    ws.Range("F3:AI3").Style.Font.FontColor = XLColor.DimGray;
                    ws.Range("F3:AI3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("AJ2:AK2").Merge().Value = $"MES: {mesTexto}";
                    ws.Range("AJ3:AK3").Merge().Value = $"AÑO: {anioStr}";
                    ws.Range("AJ4:AK4").Merge().Value = $"GENERADO: {DateTime.Now:dd-MM-yyyy}";

                    var cajaP = ws.Range("AJ2:AK4");
                    cajaP.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Font.FontSize = 9;
                    cajaP.Style.Font.Bold = true;
                    cajaP.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 244, 248);
                    cajaP.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cajaP.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Range("C6:G7").Merge().Value = $"TOTAL EMPLEADOS: {totalEmpleados}";
                    ws.Range("C6:G7").Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
                    ws.Range("C6:G7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("C6:G7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("C6:G7").Style.Font.Bold = true;
                    ws.Range("C6:G7").Style.Font.FontSize = 11;
                    ws.Range("C6:G7").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

                    ws.Range("K6:S7").Merge().Value = $"% DE ASISTENCIA MENSUAL: {porcentajeAsistencia}%";
                    ws.Range("K6:S7").Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
                    ws.Range("K6:S7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("K6:S7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("K6:S7").Style.Font.Bold = true;
                    ws.Range("K6:S7").Style.Font.FontSize = 11;
                    ws.Range("K6:S7").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

                    int filaHeader = 10;
                    ws.Cell(filaHeader, 1).Value = "ID";
                    ws.Cell(filaHeader, 2).Value = "Nombre";
                    ws.Cell(filaHeader, 3).Value = "RUT";
                    ws.Cell(filaHeader, 4).Value = "Cargo";

                    for (int d = 1; d <= totalDiasMes; d++)
                    {
                        DateTime fechaDia = new DateTime(anioInt, mesInt, d);
                        string inicialDia = fechaDia.ToString("ddd").Substring(0, 1).ToUpper();

                        ws.Cell(filaHeader - 1, 4 + d).Value = inicialDia;
                        ws.Cell(filaHeader - 1, 4 + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(filaHeader - 1, 4 + d).Style.Font.Bold = true;
                        ws.Cell(filaHeader - 1, 4 + d).Style.Font.FontSize = 8;
                        ws.Cell(filaHeader - 1, 4 + d).Style.Fill.BackgroundColor = XLColor.FromArgb(230, 235, 245);
                        ws.Cell(filaHeader - 1, 4 + d).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        ws.Cell(filaHeader - 1, 4 + d).Style.Border.OutsideBorderColor = XLColor.LightGray;

                        ws.Cell(filaHeader, 4 + d).Value = d;
                        ws.Cell(filaHeader, 4 + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(filaHeader, 4 + d).Style.Font.FontSize = 9;
                        ws.Cell(filaHeader, 4 + d).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        ws.Cell(filaHeader, 4 + d).Style.Border.OutsideBorderColor = XLColor.LightGray;

                        ws.Column(4 + d).Width = 3.2;
                    }

                    int colActual = 5 + totalDiasMes;
                    ws.Cell(filaHeader, colActual++).Value = "Presentes";
                    ws.Cell(filaHeader, colActual++).Value = "Atrasos";
                    ws.Cell(filaHeader, colActual++).Value = "Inasistencias";
                    ws.Cell(filaHeader, colActual++).Value = "Observación";

                    var rHead = ws.Range(filaHeader, 1, filaHeader, colActual - 1);
                    rHead.Style.Fill.BackgroundColor = XLColor.FromArgb(25, 60, 110);
                    rHead.Style.Font.FontColor = XLColor.White;
                    rHead.Style.Font.Bold = true;
                    rHead.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rHead.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    int filaActual = 11;
                    int idContador = 1;
                    foreach (DataRow userRow in dtUsuarios.Rows)
                    {
                        long idUsuario = Convert.ToInt64(userRow["id_usuario"]);
                        string rut = userRow["rut"].ToString() ?? "";
                        string nombre = $"{userRow["nombre"]} {userRow["apellido_paterno"]}";
                        string cargo = userRow["cargo"].ToString() ?? "";

                        ws.Cell(filaActual, 1).Value = idContador;
                        ws.Cell(filaActual, 2).Value = nombre;
                        ws.Cell(filaActual, 3).Value = rut;
                        ws.Cell(filaActual, 4).Value = cargo;
                        ws.Cell(filaActual, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(filaActual, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        int countPresentes = 0;
                        int countAtrasos = 0;
                        int countInasistencias = 0;

                        for (int d = 1; d <= totalDiasMes; d++)
                        {
                            var cellDia = ws.Cell(filaActual, 4 + d);
                            cellDia.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            cellDia.Style.Font.FontSize = 8;

                            DateTime fechaActual = new DateTime(anioInt, mesInt, d);
                            bool esFinDeSemana = (fechaActual.DayOfWeek == DayOfWeek.Saturday || fechaActual.DayOfWeek == DayOfWeek.Sunday);

                            if (esFinDeSemana)
                            {
                                cellDia.Value = "-";
                                cellDia.Style.Font.FontColor = XLColor.Gray;
                            }
                            else if (fechaActual > DateTime.Today)
                            {
                                cellDia.Value = "";
                            }
                            else
                            {
                                DataRow[] asistenciasDia = dtAsistencias.Select($"id_usuario = {idUsuario} AND dia = {d}");
                                bool tieneMarcacion = asistenciasDia.Length > 0 &&
                                                      asistenciasDia[0]["hora_entrada"] != DBNull.Value &&
                                                      asistenciasDia[0]["estado_asistencia"]?.ToString() != "Inasistente";

                                if (tieneMarcacion)
                                {
                                    string horaEntrada = asistenciasDia[0]["hora_entrada"].ToString() ?? "09:00:00";
                                    if (TimeSpan.TryParse(horaEntrada, out TimeSpan hora) && hora > new TimeSpan(9, 30, 0))
                                    {
                                        cellDia.Value = "A";
                                        cellDia.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                                        cellDia.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
                                        cellDia.Style.Font.Bold = true;
                                        countAtrasos++;
                                    }
                                    else
                                    {
                                        cellDia.Value = "P";
                                        cellDia.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 252, 231);
                                        cellDia.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61);
                                        countPresentes++;
                                    }
                                }
                                else
                                {
                                    cellDia.Value = "F";
                                    cellDia.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                                    cellDia.Style.Font.FontColor = XLColor.FromArgb(185, 28, 28);
                                    cellDia.Style.Font.Bold = true;
                                    countInasistencias++;
                                }
                            }
                            cellDia.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cellDia.Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }

                        int colT = 5 + totalDiasMes;
                        ws.Cell(filaActual, colT++).Value = countPresentes;
                        ws.Cell(filaActual, colT++).Value = countAtrasos;
                        ws.Cell(filaActual, colT++).Value = countInasistencias;

                        var cellEstado = ws.Cell(filaActual, colT++);
                        cellEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cellEstado.Style.Font.Bold = true;

                        if (countInasistencias > 2 || countAtrasos > 3)
                        {
                            cellEstado.Value = "Requiere atención";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(185, 28, 28);
                        }
                        else if (countAtrasos > 0)
                        {
                            cellEstado.Value = "Muy bueno";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
                        }
                        else
                        {
                            cellEstado.Value = "Excelente";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 252, 231);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61);
                        }

                        for (int c = 1; c <= 4; c++)
                        {
                            ws.Cell(filaActual, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(filaActual, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }

                        filaActual++;
                        idContador++;
                    }

                    int filaFirma = filaActual + 3;
                    ws.Range(filaFirma, 3, filaFirma, 8).Merge().Value = "__________________________________________________";
                    ws.Range(filaFirma, 3, filaFirma, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(filaFirma, 3, filaFirma, 8).Style.Font.Bold = true;

                    ws.Range(filaFirma + 1, 3, filaFirma + 1, 8).Merge().Value = "DIRECTOR / JEFE DE RECURSOS HUMANOS";
                    ws.Range(filaFirma + 1, 3, filaFirma + 1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(filaFirma + 1, 3, filaFirma + 1, 8).Style.Font.Bold = true;
                    ws.Range(filaFirma + 1, 3, filaFirma + 1, 8).Style.Font.FontSize = 9;
                    ws.Range(filaFirma + 1, 3, filaFirma + 1, 8).Style.Font.FontColor = XLColor.DimGray;

                    workbook.SaveAs(rutaArchivo);
                }
            }
        }

        private void GenerarExcelInasistenciasConDiseno(string rutaArchivo, int mesInt, int anioInt, string mesTexto, string anioStr)
        {
            ConexionBD conexionBD = new ConexionBD();
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                con.Open();
                string queryUsuarios = "SELECT u.id_usuario, u.rut, u.nombre, u.apellido_paterno, r.nombre_rol AS cargo FROM usuario u INNER JOIN rol r ON u.id_rol = r.id_rol WHERE u.estado_laboral = 'Vigente'";
                DataTable dtUsuarios = new DataTable();
                using (MySqlDataAdapter adapterU = new MySqlDataAdapter(queryUsuarios, con)) { adapterU.Fill(dtUsuarios); }

                int totalEmpleados = dtUsuarios.Rows.Count;
                int totalDiasMes = DateTime.DaysInMonth(anioInt, mesInt);

                string queryAsistencias = "SELECT id_usuario, DAY(fecha) AS dia, fecha, hora_entrada, estado_asistencia FROM asistencia WHERE MONTH(fecha) = @mes AND YEAR(fecha) = @anio";
                DataTable dtAsistencias = new DataTable();
                using (MySqlCommand cmdA = new MySqlCommand(queryAsistencias, con))
                {
                    cmdA.Parameters.AddWithValue("@mes", mesInt);
                    cmdA.Parameters.AddWithValue("@anio", anioInt);
                    using (MySqlDataAdapter adapterA = new MySqlDataAdapter(cmdA)) { adapterA.Fill(dtAsistencias); }
                }

                int diasHastaHoy = (anioInt == DateTime.Today.Year && mesInt == DateTime.Today.Month) ? DateTime.Today.Day : totalDiasMes;
                if (diasHastaHoy > totalDiasMes) diasHastaHoy = totalDiasMes;

                DataTable dtResumenFaltas = new DataTable();
                dtResumenFaltas.Columns.Add("id_usuario", typeof(long));
                dtResumenFaltas.Columns.Add("total_faltas", typeof(int));

                foreach (DataRow uRow in dtUsuarios.Rows)
                {
                    long idUsr = Convert.ToInt64(uRow["id_usuario"]);
                    int countFaltas = 0;
                    for (int d = 1; d <= diasHastaHoy; d++)
                    {
                        DateTime dtCheck = new DateTime(anioInt, mesInt, d);
                        if (dtCheck.DayOfWeek == DayOfWeek.Saturday || dtCheck.DayOfWeek == DayOfWeek.Sunday) continue;

                        DataRow[] rows = dtAsistencias.Select($"id_usuario = {idUsr} AND dia = {d}");
                        bool esInasistente = rows.Length == 0 ||
                                             rows[0]["estado_asistencia"]?.ToString() == "Inasistente" ||
                                             rows[0]["hora_entrada"] == DBNull.Value;
                        if (esInasistente) countFaltas++;
                    }
                    dtResumenFaltas.Rows.Add(idUsr, countFaltas);
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Inasistencias");
                    ws.TabColor = XLColor.FromArgb(20, 60, 120);
                    ws.ShowGridLines = true;

                    ws.Range("B2:G2").Merge().Value = "REPORTE DETALLADO DE INASISTENCIAS";
                    ws.Range("B2:G2").Style.Font.Bold = true;
                    ws.Range("B2:G2").Style.Font.FontSize = 18;
                    ws.Range("B2:G2").Style.Font.FontColor = XLColor.FromArgb(20, 50, 100);
                    ws.Range("B2:G2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("B3:G3").Merge().Value = $"{mesTexto.ToUpper()} {anioStr}";
                    ws.Range("B3:G3").Style.Font.Bold = true;
                    ws.Range("B3:G3").Style.Font.FontSize = 11;
                    ws.Range("B3:G3").Style.Font.FontColor = XLColor.DimGray;
                    ws.Range("B3:G3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("L2:M2").Merge().Value = $"MES: {mesTexto}";
                    ws.Range("L3:M3").Merge().Value = $"AÑO: {anioStr}";
                    ws.Range("L4:M4").Merge().Value = $"GENERADO: {DateTime.Now:dd-MM-yyyy}";

                    var cajaP = ws.Range("L2:M4");
                    cajaP.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Font.FontSize = 9;
                    cajaP.Style.Font.Bold = true;
                    cajaP.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 244, 248);
                    cajaP.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cajaP.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Range("B6:D7").Merge().Value = $"TOTAL EMPLEADOS: {totalEmpleados}";
                    ws.Range("B6:D7").Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
                    ws.Range("B6:D7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("B6:D7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("B6:D7").Style.Font.Bold = true;
                    ws.Range("B6:D7").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

                    int fHeadDetalle = 9;
                    ws.Cell(fHeadDetalle, 2).Value = "ID";
                    ws.Cell(fHeadDetalle, 3).Value = "Nombre";
                    ws.Cell(fHeadDetalle, 4).Value = "RUT";
                    ws.Cell(fHeadDetalle, 5).Value = "Cargo";
                    ws.Cell(fHeadDetalle, 6).Value = "Fecha Inasistencia";
                    ws.Cell(fHeadDetalle, 7).Value = "Estado";

                    var rHeadDetalle = ws.Range(fHeadDetalle, 2, fHeadDetalle, 7);
                    rHeadDetalle.Style.Fill.BackgroundColor = XLColor.FromArgb(25, 60, 110);
                    rHeadDetalle.Style.Font.FontColor = XLColor.White;
                    rHeadDetalle.Style.Font.Bold = true;
                    rHeadDetalle.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rHeadDetalle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    int fDataDetalle = 10;
                    foreach (DataRow uRow in dtUsuarios.Rows)
                    {
                        long idUsr = Convert.ToInt64(uRow["id_usuario"]);
                        string nombre = $"{uRow["nombre"]} {uRow["apellido_paterno"]}";
                        string rut = uRow["rut"].ToString() ?? "";
                        string cargo = uRow["cargo"].ToString() ?? "";

                        for (int d = diasHastaHoy; d >= 1; d--)
                        {
                            DateTime dtCheck = new DateTime(anioInt, mesInt, d);
                            if (dtCheck.DayOfWeek == DayOfWeek.Saturday || dtCheck.DayOfWeek == DayOfWeek.Sunday) continue;

                            DataRow[] rows = dtAsistencias.Select($"id_usuario = {idUsr} AND dia = {d}");
                            bool esInasistente = rows.Length == 0 ||
                                                 rows[0]["estado_asistencia"]?.ToString() == "Inasistente" ||
                                                 rows[0]["hora_entrada"] == DBNull.Value;

                            if (esInasistente)
                            {
                                ws.Cell(fDataDetalle, 2).Value = idUsr;
                                ws.Cell(fDataDetalle, 3).Value = nombre;
                                ws.Cell(fDataDetalle, 4).Value = rut;
                                ws.Cell(fDataDetalle, 5).Value = cargo;
                                ws.Cell(fDataDetalle, 6).Value = dtCheck.ToString("dd/MM/yyyy");
                                ws.Cell(fDataDetalle, 7).Value = "Inasistente";

                                ws.Cell(fDataDetalle, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Cell(fDataDetalle, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Cell(fDataDetalle, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Cell(fDataDetalle, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                                for (int c = 2; c <= 7; c++)
                                {
                                    ws.Cell(fDataDetalle, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                    ws.Cell(fDataDetalle, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                                }
                                fDataDetalle++;
                            }
                        }
                    }

                    int fHeadResumen = 9;
                    ws.Cell(fHeadResumen, 11).Value = "ID";
                    ws.Cell(fHeadResumen, 12).Value = "Nombre";
                    ws.Cell(fHeadResumen, 13).Value = "Cantidad Inasistencias";
                    ws.Cell(fHeadResumen, 14).Value = "Observación";

                    var rHeadResumen = ws.Range(fHeadResumen, 11, fHeadResumen, 14);
                    rHeadResumen.Style.Fill.BackgroundColor = XLColor.FromArgb(93, 109, 126);
                    rHeadResumen.Style.Font.FontColor = XLColor.White;
                    rHeadResumen.Style.Font.Bold = true;
                    rHeadResumen.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rHeadResumen.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    int fDataResumen = 10;
                    int rowIndex = 0;
                    foreach (DataRow uRow in dtUsuarios.Rows)
                    {
                        long idUsuario = Convert.ToInt64(uRow["id_usuario"]);
                        string nombre = $"{uRow["nombre"]} {uRow["apellido_paterno"]}";
                        int cantidadMetrica = 0;
                        DataRow[] mRows = dtResumenFaltas.Select($"id_usuario = {idUsuario}");
                        if (mRows.Length > 0) cantidadMetrica = Convert.ToInt32(mRows[0]["total_faltas"]);

                        ws.Cell(fDataResumen, 11).Value = idUsuario;
                        ws.Cell(fDataResumen, 12).Value = nombre;
                        ws.Cell(fDataResumen, 13).Value = cantidadMetrica;

                        ws.Cell(fDataResumen, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(fDataResumen, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        var colorFondoFila = (rowIndex % 2 == 0) ? XLColor.FromArgb(244, 246, 249) : XLColor.FromArgb(235, 240, 245);
                        ws.Range(fDataResumen, 11, fDataResumen, 13).Style.Fill.BackgroundColor = colorFondoFila;

                        var cellEstado = ws.Cell(fDataResumen, 14);
                        cellEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cellEstado.Style.Font.Bold = true;

                        if (cantidadMetrica >= 3)
                        {
                            cellEstado.Value = "Requiere atención";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(185, 28, 28);
                        }
                        else if (cantidadMetrica > 0)
                        {
                            cellEstado.Value = "Muy bueno";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
                        }
                        else
                        {
                            cellEstado.Value = "Excelente";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 252, 231);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61);
                        }

                        for (int c = 11; c <= 14; c++)
                        {
                            ws.Cell(fDataResumen, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(fDataResumen, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }
                        fDataResumen++;
                        rowIndex++;
                    }

                    int maxFilas = Math.Max(fDataDetalle, fDataResumen);
                    int fFirma = maxFilas + 3;
                    ws.Range(fFirma, 3, fFirma, 6).Merge().Value = "__________________________________________________";
                    ws.Range(fFirma, 3, fFirma, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma, 3, fFirma, 6).Style.Font.Bold = true;

                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Merge().Value = "DIRECTOR / JEFE DE RECURSOS HUMANOS";
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.Bold = true;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.FontSize = 9;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.FontColor = XLColor.DimGray;

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(rutaArchivo);
                }
            }
        }

        private void GenerarReporteConDobleTablaCompleto(string tituloReporte, string rutaArchivo, string queryDetalle, string colFechaNombre, string colDatoNombre, string queryResumen, int mesInt, int anioInt, string mesTexto, string anioStr)
        {
            ConexionBD conexionBD = new ConexionBD();
            using (MySqlConnection con = conexionBD.ObtenerConexion())
            {
                con.Open();

                string queryUsuarios = "SELECT u.id_usuario, u.rut, u.nombre, u.apellido_paterno, r.nombre_rol AS cargo FROM usuario u INNER JOIN rol r ON u.id_rol = r.id_rol WHERE u.estado_laboral = 'Vigente'";
                DataTable dtUsuarios = new DataTable();
                using (MySqlDataAdapter adapterU = new MySqlDataAdapter(queryUsuarios, con)) { adapterU.Fill(dtUsuarios); }

                int totalEmpleados = dtUsuarios.Rows.Count;

                DataTable dtDetalle = new DataTable();
                using (MySqlCommand cmd = new MySqlCommand(queryDetalle, con))
                {
                    cmd.Parameters.AddWithValue("@mes", mesInt);
                    cmd.Parameters.AddWithValue("@anio", anioInt);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd)) { adapter.Fill(dtDetalle); }
                }

                DataTable dtResumen = new DataTable();
                using (MySqlCommand cmdRes = new MySqlCommand(queryResumen, con))
                {
                    cmdRes.Parameters.AddWithValue("@mes", mesInt);
                    cmdRes.Parameters.AddWithValue("@anio", anioInt);
                    using (MySqlDataAdapter adapterRes = new MySqlDataAdapter(cmdRes)) { adapterRes.Fill(dtResumen); }
                }

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Reporte");
                    ws.TabColor = XLColor.FromArgb(20, 60, 120);
                    ws.ShowGridLines = true;

                    ws.Range("B2:G2").Merge().Value = tituloReporte;
                    ws.Range("B2:G2").Style.Font.Bold = true;
                    ws.Range("B2:G2").Style.Font.FontSize = 18;
                    ws.Range("B2:G2").Style.Font.FontColor = XLColor.FromArgb(20, 50, 100);
                    ws.Range("B2:G2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("B3:G3").Merge().Value = $"{mesTexto.ToUpper()} {anioStr}";
                    ws.Range("B3:G3").Style.Font.Bold = true;
                    ws.Range("B3:G3").Style.Font.FontSize = 11;
                    ws.Range("B3:G3").Style.Font.FontColor = XLColor.DimGray;
                    ws.Range("B3:G3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Range("L2:M2").Merge().Value = $"MES: {mesTexto}";
                    ws.Range("L3:M3").Merge().Value = $"AÑO: {anioStr}";
                    ws.Range("L4:M4").Merge().Value = $"GENERADO: {DateTime.Now:dd-MM-yyyy}";

                    var cajaP = ws.Range("L2:M4");
                    cajaP.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    cajaP.Style.Font.FontSize = 9;
                    cajaP.Style.Font.Bold = true;
                    cajaP.Style.Fill.BackgroundColor = XLColor.FromArgb(240, 244, 248);
                    cajaP.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cajaP.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Range("B6:D7").Merge().Value = $"TOTAL EMPLEADOS: {totalEmpleados}";
                    ws.Range("B6:D7").Style.Fill.BackgroundColor = XLColor.FromArgb(245, 247, 250);
                    ws.Range("B6:D7").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range("B6:D7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range("B6:D7").Style.Font.Bold = true;
                    ws.Range("B6:D7").Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

                    int fHeadDetalle = 9;
                    ws.Cell(fHeadDetalle, 2).Value = "ID";
                    ws.Cell(fHeadDetalle, 3).Value = "Nombre";
                    ws.Cell(fHeadDetalle, 4).Value = "RUT";
                    ws.Cell(fHeadDetalle, 5).Value = "Cargo";
                    ws.Cell(fHeadDetalle, 6).Value = colFechaNombre;
                    ws.Cell(fHeadDetalle, 7).Value = colDatoNombre;

                    var rHeadDetalle = ws.Range(fHeadDetalle, 2, fHeadDetalle, 7);
                    rHeadDetalle.Style.Fill.BackgroundColor = XLColor.FromArgb(25, 60, 110);
                    rHeadDetalle.Style.Font.FontColor = XLColor.White;
                    rHeadDetalle.Style.Font.Bold = true;
                    rHeadDetalle.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rHeadDetalle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    int fDataDetalle = 10;
                    foreach (DataRow row in dtDetalle.Rows)
                    {
                        long idUsuario = Convert.ToInt64(row["id_usuario"]);
                        string nombre = $"{row["nombre"]} {row["apellido_paterno"]}";
                        string rut = row["rut"].ToString() ?? "";
                        string cargo = row["Cargo"].ToString() ?? "";
                        string fecha = Convert.ToDateTime(row["Fecha"]).ToString("dd/MM/yyyy");
                        string datoExtra = (row.Table.Columns.Contains("HoraEntrada") ? row["HoraEntrada"] : row["HoraSalida"])?.ToString() ?? "";

                        ws.Cell(fDataDetalle, 2).Value = idUsuario;
                        ws.Cell(fDataDetalle, 3).Value = nombre;
                        ws.Cell(fDataDetalle, 4).Value = rut;
                        ws.Cell(fDataDetalle, 5).Value = cargo;
                        ws.Cell(fDataDetalle, 6).Value = fecha;
                        ws.Cell(fDataDetalle, 7).Value = datoExtra;

                        ws.Cell(fDataDetalle, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(fDataDetalle, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(fDataDetalle, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(fDataDetalle, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        for (int c = 2; c <= 7; c++)
                        {
                            ws.Cell(fDataDetalle, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(fDataDetalle, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }
                        fDataDetalle++;
                    }

                    int fHeadResumen = 9;
                    ws.Cell(fHeadResumen, 11).Value = "ID";
                    ws.Cell(fHeadResumen, 12).Value = "Nombre";
                    ws.Cell(fHeadResumen, 13).Value = (tituloReporte.Contains("ATRASOS") ? "Cantidad de Atrasos" : "Cantidad Salidas Anticipadas");
                    ws.Cell(fHeadResumen, 14).Value = "Observación";

                    var rHeadResumen = ws.Range(fHeadResumen, 11, fHeadResumen, 14);
                    rHeadResumen.Style.Fill.BackgroundColor = XLColor.FromArgb(93, 109, 126);
                    rHeadResumen.Style.Font.FontColor = XLColor.White;
                    rHeadResumen.Style.Font.Bold = true;
                    rHeadResumen.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    rHeadResumen.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    int fDataResumen = 10;
                    int rowIndex = 0;
                    foreach (DataRow uRow in dtUsuarios.Rows)
                    {
                        long idUsuario = Convert.ToInt64(uRow["id_usuario"]);
                        string nombre = $"{uRow["nombre"]} {uRow["apellido_paterno"]}";
                        int cantidadMetrica = 0;
                        DataRow[] mRows = dtResumen.Select($"id_usuario = {idUsuario}");
                        if (mRows.Length > 0) cantidadMetrica = Convert.ToInt32(mRows[0]["total_valor"]);

                        ws.Cell(fDataResumen, 11).Value = idUsuario;
                        ws.Cell(fDataResumen, 12).Value = nombre;
                        ws.Cell(fDataResumen, 13).Value = cantidadMetrica;

                        ws.Cell(fDataResumen, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Cell(fDataResumen, 13).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        var colorFondoFila = (rowIndex % 2 == 0) ? XLColor.FromArgb(244, 246, 249) : XLColor.FromArgb(235, 240, 245);
                        ws.Range(fDataResumen, 11, fDataResumen, 13).Style.Fill.BackgroundColor = colorFondoFila;

                        var cellEstado = ws.Cell(fDataResumen, 14);
                        cellEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cellEstado.Style.Font.Bold = true;

                        if (cantidadMetrica >= 3)
                        {
                            cellEstado.Value = "Requiere atención";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 226, 226);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(185, 28, 28);
                        }
                        else if (cantidadMetrica > 0)
                        {
                            cellEstado.Value = "Muy bueno";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(254, 243, 199);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(180, 83, 9);
                        }
                        else
                        {
                            cellEstado.Value = "Excelente";
                            cellEstado.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 252, 231);
                            cellEstado.Style.Font.FontColor = XLColor.FromArgb(21, 128, 61);
                        }

                        for (int c = 11; c <= 14; c++)
                        {
                            ws.Cell(fDataResumen, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Cell(fDataResumen, c).Style.Border.OutsideBorderColor = XLColor.LightGray;
                        }
                        fDataResumen++;
                        rowIndex++;
                    }

                    int maxFilas = Math.Max(fDataDetalle, fDataResumen);
                    int fFirma = maxFilas + 3;
                    ws.Range(fFirma, 3, fFirma, 6).Merge().Value = "__________________________________________________";
                    ws.Range(fFirma, 3, fFirma, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma, 3, fFirma, 6).Style.Font.Bold = true;

                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Merge().Value = "DIRECTOR / JEFE DE RECURSOS HUMANOS";
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.Bold = true;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.FontSize = 9;
                    ws.Range(fFirma + 1, 3, fFirma + 1, 6).Style.Font.FontColor = XLColor.DimGray;

                    ws.Columns().AdjustToContents();
                    workbook.SaveAs(rutaArchivo);
                }
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            PanelPrincipalVentana panel = new PanelPrincipalVentana(usuarioLogueado);
            panel.Show();
            this.Close();
        }

        private void BtnVolverGestion_Click(object sender, RoutedEventArgs e)
        {
            VentanaGestionUsuarios gestion = new VentanaGestionUsuarios(usuarioLogueado);
            gestion.Show();
            this.Close();
        }
    }
}