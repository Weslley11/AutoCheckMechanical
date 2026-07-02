using System.Windows;
using AutoCheckMechanical.Core;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using AutoCheckMechanical.Models;
using CheckContextModel = AutoCheckMechanical.Core.CheckContext;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical
{
    public partial class MainWindow : Window
    {
        private bool _isPseudoMaximized;
        private Rect _restoreBounds;

        public MainWindow()
        {
            InitializeComponent();

            txtUser.Text = Environment.UserName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                MessageBox.Show("SolidWorks não encontrado.");
                return;
            }

            if (session.ActiveDocument == null)
            {
                MessageBox.Show("Nenhum documento aberto.");
                return;
            }

            MessageBox.Show("Conectado!\n\n" + session.ActiveDocument.GetTitle());
        }

        private void AddLog(string texto)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + texto + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        private SolidWorksSession RefreshConnectionStatus()
        {
            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                AddLog("SolidWorks não encontrado.");
                txtStatus.Text = "SolidWorks desconectado.";

                ledSolidWorks.Fill = Brushes.Red;
                txtSolidWorks.Text = "Desconectado";

                return session;
            }

            ledSolidWorks.Fill = Brushes.LimeGreen;
            txtSolidWorks.Text = "Conectado";

            AddLog("SolidWorks conectado.");

            if (session.ActiveDocument == null)
            {
                AddLog("Nenhum documento aberto.");
                txtStatus.Text = "Nenhum documento.";
                return session;
            }

            txtArquivo.Text = session.ActiveDocument.GetTitle();

            AddLog("Arquivo:");
            AddLog(session.ActiveDocument.GetTitle());

            return session;
        }

        private void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            AddLog("Verificando conexão...");

            RefreshConnectionStatus();
        }

        private void BtnCheckDrawing_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            AddLog("Iniciando AutoCheck...");

            SolidWorksSession session = RefreshConnectionStatus();

            if (!session.IsConnected || session.ActiveDocument == null)
                return;

            CheckContextModel context = new CheckContextModel(session.Application, session.ActiveDocument);

            CheckEngine engine = new CheckEngine();

            CheckerManager.Register(engine);

            btnCheckDrawing.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                List<CheckResult> results = engine.Execute(context);

                AddLog("--------------------------------");

                foreach (CheckResult result in results)
                {
                    AddLog(result.Checker);

                    if (result.Success)
                    {
                        AddLog("OK");

                        if (!string.IsNullOrEmpty(result.Message))
                            AddLog(result.Message);
                    }
                    else
                    {
                        AddLog("ERRO");

                        foreach (string erro in result.Errors)
                            AddLog(erro);
                    }

                    foreach (string log in result.Logs)
                        AddLog(log);

                    AddLog("--------------------------------");
                }

                txtStatus.Text = "Check finalizado.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                btnCheckDrawing.IsEnabled = true;
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_isPseudoMaximized)
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;

                _isPseudoMaximized = false;
            }
            else
            {
                _restoreBounds = new Rect(Left, Top, Width, Height);

                Rect workArea = SystemParameters.WorkArea;

                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;

                _isPseudoMaximized = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
