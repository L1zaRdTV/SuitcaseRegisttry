using SuitcaseRegisttry.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SuitcaseRegisttry
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
            public MainWindow()
            {
                InitializeComponent();
                InitializedDatabaseConnection();
                SetupNavigationSystem();
                LoadAuthorizationPage();

            }
            private void InitializedDatabaseConnection()
            {
                try
                {
                    AppData.AppConnect.Modelo11 = new AppData.SuitcaseRegistryEntities2();
                }
                catch(System.Exception ex) 
                {
                    MessageBox.Show($"НЕ УДАЛОСЬ ПОДКЛЮЧТСЯ К БАЗЕ ДАННЫХ: \n{ex.Message}", "СИСТЕМА ТОГО", MessageBoxButton.OK, MessageBoxImage.Error);   
                }
            }
            private void SetupNavigationSystem()
            {
                AppData.AppFrame.FrameMain = FrmMain;
            }
            private void LoadAuthorizationPage()
            {
                FrmMain.Navigate(new Autorizaiton());
            }
    }
}

