using SuitcaseRegisttry.AppData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        private readonly List<SuitcaseViewItem> _suitcases = new List<SuitcaseViewItem>();
        private readonly List<TrackingViewItem> _tracking = new List<TrackingViewItem>();

        public Autorizaiton()
        {
            InitializeComponent();
            LoadDemoData();
            RefreshTables();
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

            ProcessSuitcase(suitcase, cbEscape.IsChecked == true);

            var viewItem = new SuitcaseViewItem
            {
                QrKod = suitcase.QRKod,
                FlightCode = txtFlight.Text.Trim(),
                StatusId = suitcase.IDStatys ?? StatusRegistered,
                IsDangerous = suitcase.SignDanger == "Опасно",
                DangerText = suitcase.SignDanger
            };
            viewItem.UpdateStatusVisual();
            _suitcases.Insert(0, viewItem);

            AddTracking($"{viewItem.QrKod}: статус изменён на '{viewItem.StatusText}'");
            txtStatus.Text = $"Статус: {GetStatusName(suitcase.IDStatys)}";

            RefreshTables();
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

        private void ProcessSuitcase(Suitcase suitcase, bool isEscape)
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
                CreateIncident(suitcase, isDangerous, isOverweight, false);
            }
            else
            {
                suitcase.IDStatys = StatusApproved;
            }

            if (isEscape)
            {
                suitcase.IDStatys = StatusInspection;
                CreateIncident(suitcase, false, false, true);
                AddTracking($"{suitcase.QRKod}: событие 'Побег'. Создан INCIDENT");
            }

            suitcase.Last_Up = DateTime.Now;
        }

        private void CreateIncident(Suitcase suitcase, bool isDangerous, bool isOverweight, bool isEscape)
        {
            var description = "";

            if (isEscape)
            {
                description = "Побег чемодана из зоны контроля.";
            }
            else if (isDangerous && isOverweight)
            {
                description = "Опасный и слишком тяжелый чемодан.";
            }
            else if (isDangerous)
            {
                description = "Опасные признаки в чемодане.";
            }
            else if (isOverweight)
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

        private void BtnTrack_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgPassenger.SelectedItem as SuitcaseViewItem;
            if (selected == null)
            {
                MessageBox.Show("Выбери чемодан в таблице пассажира.", "Трекинг", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddTracking($"Отследить: {selected.QrKod}, рейс {selected.FlightCode}, статус {selected.StatusText}");
            RefreshTables();
        }

        private void AddTracking(string description)
        {
            _tracking.Insert(0, new TrackingViewItem
            {
                TimeText = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                Description = description
            });
        }

        private void LoadDemoData()
        {
            _suitcases.Add(new SuitcaseViewItem
            {
                QrKod = "QR-100501",
                FlightCode = "MSK-77",
                StatusId = StatusApproved,
                IsDangerous = false,
                DangerText = "Норма"
            });
            _suitcases.Add(new SuitcaseViewItem
            {
                QrKod = "QR-100777",
                FlightCode = "KZN-12",
                StatusId = StatusInspection,
                IsDangerous = true,
                DangerText = "Опасно"
            });

            foreach (var item in _suitcases)
            {
                item.UpdateStatusVisual();
            }

            AddTracking("QR-100501: прибыл в пункт досмотра");
            AddTracking("QR-100777: отправлен инспектору");
        }

        private void RefreshTables()
        {
            dgPassenger.ItemsSource = null;
            dgPassenger.ItemsSource = _suitcases;

            dgInspector.ItemsSource = null;
            dgInspector.ItemsSource = _suitcases;

            lbTracking.ItemsSource = null;
            lbTracking.ItemsSource = _tracking;
        }

        private class SuitcaseViewItem
        {
            public string QrKod { get; set; }
            public string FlightCode { get; set; }
            public int StatusId { get; set; }
            public string StatusText { get; set; }
            public Brush StatusColor { get; set; }
            public bool IsDangerous { get; set; }
            public string DangerText { get; set; }

            public void UpdateStatusVisual()
            {
                switch (StatusId)
                {
                    case StatusInspection:
                        StatusText = "На досмотре";
                        StatusColor = Brushes.IndianRed;
                        break;
                    case StatusApproved:
                        StatusText = "Допущен";
                        StatusColor = Brushes.SeaGreen;
                        break;
                    default:
                        StatusText = "Зарегистрирован";
                        StatusColor = Brushes.SteelBlue;
                        break;
                }
            }
        }

        private class TrackingViewItem
        {
            public string TimeText { get; set; }
            public string Description { get; set; }
        }
    }
}
