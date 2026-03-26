using SuitcaseRegisttry.AppData;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SuitcaseRegisttry.Pages
{
    /// <summary>
    /// Логика взаимодействия для Autorizaiton.xaml
    /// </summary>
    public partial class Autorizaiton : Page
    {
        private const int StatusRegistered = 1;
        private const int StatusInspection = 2;
        private const int StatusApproved = 3;

        public Autorizaiton()
        {
            InitializeComponent();
        }

        private void BtnCheckSuitcase_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
            {
                return;
            }

            var suitcase = new Suitcase
            {
                QRKod = $"QR-{DateTime.Now:HHmmss}",
                IDStatys = StatusRegistered,
                DateReg = DateTime.Now,
                Last_Up = DateTime.Now,
                SignDanger = cbDangerous.IsChecked == true ? "Опасно" : "Норма",
                Weight = cbDangerous.IsChecked == true ? 36 : 18
            };

            ProcessSuitcase(suitcase);

            txtStatus.Text = $"Статус: {GetStatusName(suitcase.IDStatys)}";
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtFIO.Text) || string.IsNullOrWhiteSpace(txtRoles.Text))
            {
                MessageBox.Show("Заполни ФИО и роль.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtEmail.Text) || !txtEmail.Text.Contains("@"))
            {
                MessageBox.Show("Email выглядит некорректно.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtFlight.Text))
            {
                MessageBox.Show("Нужен номер рейса.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ProcessSuitcase(Suitcase suitcase)
        {
            if (suitcase == null)
            {
                return;
            }

            var isDangerous = suitcase.SignDanger == "Опасно";
            var isOverweight = suitcase.Weight.HasValue && suitcase.Weight.Value > 30;

            if (isDangerous || isOverweight)
            {
                suitcase.IDStatys = StatusInspection;
                CreateIncident(suitcase, isDangerous, isOverweight);
            }
            else
            {
                suitcase.IDStatys = StatusApproved;
            }

            suitcase.Last_Up = DateTime.Now;
        }

        private void CreateIncident(Suitcase suitcase, bool isDangerous, bool isOverweight)
        {
            string description;

            if (isDangerous && isOverweight)
            {
                description = "Опасный и слишком тяжелый чемодан.";
            }
            else if (isDangerous)
            {
                description = "Опасные признаки в чемодане.";
            }
            else
            {
                description = "Превышение веса чемодана.";
            }

            var incident = new Incident
            {
                IDSuitcase = suitcase.IDSuitcase,
                Date = DateTime.Now,
                Place = txtFlight.Text,
                Description = description,
                IDStatysTypeIncident = 1
            };

            if (AppConnect.Modelo11 != null)
            {
                AppConnect.Modelo11.Incident.Add(incident);
                AppConnect.Modelo11.SaveChanges();
            }
        }

        private string GetStatusName(int? statusId)
        {
            switch (statusId)
            {
                case StatusRegistered:
                    return "Зарегистрирован";
                case StatusInspection:
                    return "На досмотре";
                case StatusApproved:
                    return "Допущен к перевозке";
                default:
                    return "Неизвестно";
            }
        }
    }
}
