using System.Windows;
using AutoCheckMechanical.Core;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using AutoCheckMechanical.Checkers;
using AutoCheckMechanical.Models;
using CheckContextModel = AutoCheckMechanical.Core.CheckContext;
using AutoCheckMechanical.Services;

namespace AutoCheckMechanical
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private void BtnCheckDrawing_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            AddLog("Iniciando AutoCheck...");

            SolidWorksSession session = SolidWorksSession.Connect();

            if (!session.IsConnected)
            {
                AddLog("SolidWorks não encontrado.");
                txtStatus.Text = "SolidWorks desconectado.";

                ledSolidWorks.Fill = Brushes.Red;
                txtSolidWorks.Text = "Desconectado";
                return;
            }

            ledSolidWorks.Fill = Brushes.LimeGreen;
            txtSolidWorks.Text = "Conectado";

            AddLog("SolidWorks conectado.");

            if (session.ActiveDocument == null)
            {
                AddLog("Nenhum documento aberto.");
                txtStatus.Text = "Nenhum documento.";
                return;
            }

            txtArquivo.Text =
                session.ActiveDocument.GetTitle();

            AddLog("Arquivo:");

            AddLog(session.ActiveDocument.GetTitle());

            CheckContextModel context = new CheckContextModel(session.Application, session.ActiveDocument);

            CheckEngine engine = new CheckEngine();

            engine.Register(new FlatPatternChecker());
            engine.Register(new LayerChecker());
            engine.Register(new ScaleChecker());
            engine.Register(new DimensionChecker());
            engine.Register(new BalloonChecker());

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
    }
}