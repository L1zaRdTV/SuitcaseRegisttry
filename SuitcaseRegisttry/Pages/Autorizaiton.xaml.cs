using SuitcaseRegisttry.AppData;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SuitcaseRegisttry.Pages
{
    public partial class Autorizaiton : Page
    {
        private User _currentUser;
        private string _currentRole;

        public Autorizaiton()
        {
            InitializeComponent();
        }

        private SuitcaseRegistryEntities2 Db => AppConnect.Modelo11;

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (Db == null)
            {
                MessageBox.Show("Нет подключения к SQL Server.");
                return;
            }

            var fio = txtLoginName.Text.Trim();
            if (string.IsNullOrWhiteSpace(fio))
            {
                MessageBox.Show("Введите ФИО.");
                return;
            }

            _currentRole = ((ComboBoxItem)cbRole.SelectedItem).Content.ToString();
            _currentUser = GetOrCreateUser(fio, _currentRole);

            txtCurrentUser.Text = $"Авторизован: {_currentUser.FIO} ({_currentRole})";
            txtWelcome.Text = $"Здравствуйте, {_currentUser.FIO}!";
            txtTicket.Text = $"Номер билета: {(_currentUser.NumberTicet.HasValue ? _currentUser.NumberTicet.Value.ToString() : "не назначен")}";

            PassengerTab.Visibility = _currentRole == "Passenger" ? Visibility.Visible : Visibility.Collapsed;
            InspectorTab.Visibility = _currentRole == "Inspector" ? Visibility.Visible : Visibility.Collapsed;
            LogisticTab.Visibility = _currentRole == "Logistician" ? Visibility.Visible : Visibility.Collapsed;

            LoadPassengerSuitcases();
            LoadPassengerIncidents();
            LoadAllSuitcasesForInspector();
            LoadInspectorJournal();
            LoadLostSuitcases();
            LoadLogisticTimeline();
        }

        private User GetOrCreateUser(string fio, string roleName)
        {
            var roleId = Db.Roles.Where(r => r.Name == roleName).Select(r => r.IDRoles).FirstOrDefault();
            if (roleId == 0)
            {
                var role = new Roles { Name = roleName };
                Db.Roles.Add(role);
                Db.SaveChanges();
                roleId = role.IDRoles;
            }

            var user = Db.User.FirstOrDefault(u => u.FIO == fio && u.IDRoles == roleId);
            if (user != null)
            {
                return user;
            }

            user = new User
            {
                FIO = fio,
                IDRoles = roleId,
                DateReg = DateTime.Now,
                Token = Guid.NewGuid().ToString("N"),
                NumberTicet = new Random().Next(10000, 99999)
            };
            Db.User.Add(user);
            Db.SaveChanges();
            return user;
        }

        private void add_suitcase(int ownerId, string model, string colour, int weight, int dangerDegree)
        {
            var statusId = GetStatusId("Зарегистрирован");
            if (dangerDegree > 2)
            {
                statusId = GetStatusId("На досмотре", statusId);
            }

            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Suitcase
                  (QRKod, Owner, Model, Colour, Weight, IDDegreeDanger, IDStatys, DateReg, Last_Up, Features)
                  VALUES
                  (@qr, @owner, @model, @colour, @weight, @danger, @status, GETDATE(), GETDATE(), @features)",
                new SqlParameter("@qr", "QR-" + DateTime.Now.ToString("yyyyMMddHHmmss")),
                new SqlParameter("@owner", ownerId),
                new SqlParameter("@model", string.IsNullOrWhiteSpace(model) ? "Неизвестная модель" : model),
                new SqlParameter("@colour", string.IsNullOrWhiteSpace(colour) ? "Не указан" : colour),
                new SqlParameter("@weight", weight),
                new SqlParameter("@danger", dangerDegree),
                new SqlParameter("@status", statusId),
                new SqlParameter("@features", dangerDegree > 2 ? "Авто: направлен на досмотр" : "Обычная регистрация"));
        }

        private void inspect_suitcase(int suitcaseId, int inspectorId)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Inspection (IDSuitcase, Inspector, Date, IDTypeInspection, IDResult, Description, Conclusion)
                  VALUES (@sid, @insp, GETDATE(), @type, @result, @desc, @conc)",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@insp", inspectorId),
                new SqlParameter("@type", 1),
                new SqlParameter("@result", 1),
                new SqlParameter("@desc", "Плановый досмотр сотрудником"),
                new SqlParameter("@conc", "Запрещенных предметов не выявлено"));

            Db.Database.ExecuteSqlCommand(
                @"UPDATE Suitcase SET IDStatys = @status, Last_Up = GETDATE() WHERE IDSuitcase = @sid",
                new SqlParameter("@status", GetStatusId("Досмотр пройден", GetStatusId("На досмотре"))),
                new SqlParameter("@sid", suitcaseId));

            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Tracking (IDSuitcase, Coordinate, IDStatysTracking, Time, Scanned)
                  VALUES (@sid, @coord, @trackStatus, GETDATE(), @scanned)",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@coord", "Пункт досмотра: пройден"),
                new SqlParameter("@trackStatus", 1),
                new SqlParameter("@scanned", inspectorId));
        }

        private void confiscate_item(int suitcaseId, int inspectorId, string subject)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO ConfiscatedItem (IDSuitcase, Inspector, Subject, Quantity, Measurement, DateConfiscation, Storagelocation, Destroyed)
                  VALUES (@sid, @insp, @subject, @qty, @measurement, GETDATE(), @storage, @destroyed)",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@insp", inspectorId),
                new SqlParameter("@subject", subject),
                new SqlParameter("@qty", 1),
                new SqlParameter("@measurement", "шт"),
                new SqlParameter("@storage", "Склад службы безопасности"),
                new SqlParameter("@destroyed", 0));

            Db.Database.ExecuteSqlCommand(
                @"UPDATE Suitcase SET IDStatys = @status, Last_Up = GETDATE() WHERE IDSuitcase = @sid",
                new SqlParameter("@status", GetStatusId("Конфискован", GetStatusId("На досмотре"))),
                new SqlParameter("@sid", suitcaseId));
        }

        private void update_tracking(int suitcaseId, string coordinate)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Tracking (IDSuitcase, Coordinate, IDStatysTracking, Time, Scanned)
                  VALUES (@sid, @coord, @status, GETDATE(), @scanned)",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@coord", coordinate),
                new SqlParameter("@status", 1),
                new SqlParameter("@scanned", _currentUser != null ? (object)_currentUser.IDUser : DBNull.Value));
        }

        private void BtnAddSuitcase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null || _currentRole != "Passenger")
            {
                return;
            }

            if (!int.TryParse(txtWeight.Text, out var weight)) weight = 15;
            if (!int.TryParse(txtDanger.Text, out var danger)) danger = 1;

            add_suitcase(_currentUser.IDUser, txtModel.Text.Trim(), txtColour.Text.Trim(), weight, danger);

            if (danger > 2)
            {
                AddIncidentByRule("Опасность выше 2. Чемодан автоматически отправлен на досмотр.");
            }

            LoadPassengerSuitcases();
            LoadPassengerIncidents();
            LoadAllSuitcasesForInspector();
            MessageBox.Show("Чемодан успешно зарегистрирован.");
        }

        private void BtnTrackPassenger_Click(object sender, RoutedEventArgs e)
        {
            var selected = ResolvePassengerSelection(sender);
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан.");
                return;
            }

            var history = Db.Tracking
                .Where(t => t.IDSuitcase == selected.IDSuitcase)
                .OrderBy(t => t.Time)
                .Take(30)
                .Select(t => new
                {
                    Time = t.Time,
                    Location = t.Coordinate,
                    Status = t.StatysTracking != null ? t.StatysTracking.Name : "в обработке"
                })
                .ToList();

            lbPassengerTrack.ItemsSource = history
                .Select(t => string.Format("{0} — {1} ({2})",
                    t.Time.HasValue ? IconForStep(t.Status) + " " + t.Time.Value.ToString("HH:mm") : "⏳ --:--",
                    string.IsNullOrWhiteSpace(t.Location) ? "Локация не указана" : t.Location,
                    t.Status))
                .ToList();
        }

        private void BtnPassengerReportProblem_Click(object sender, RoutedEventArgs e)
        {
            var selected = ResolvePassengerSelection(sender);
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан для фиксации проблемы.");
                return;
            }

            Db.Incident.Add(new Incident
            {
                IDSuitcase = selected.IDSuitcase,
                Date = DateTime.Now,
                Place = "Личный кабинет пассажира",
                Description = "Пассажир сообщил о проблеме с чемоданом.",
                IDTypeIncident = 1,
                IDStatysTypeIncident = 1,
                Responsible = _currentUser != null ? (int?)_currentUser.IDUser : null
            });
            Db.SaveChanges();

            LoadPassengerIncidents();
            MessageBox.Show("Сообщение о проблеме отправлено.");
        }

        private void BtnInspect_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                return;
            }

            var selected = dgInspector.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан для досмотра.");
                return;
            }

            inspect_suitcase(selected.IDSuitcase, _currentUser.IDUser);
            LoadAllSuitcasesForInspector();
            LoadInspectorJournal();
            MessageBox.Show("Досмотр завершен.");
        }

        private void BtnConfiscate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                return;
            }

            var selected = dgInspector.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан для конфискации предмета.");
                return;
            }

            var subject = string.IsNullOrWhiteSpace(txtConfiscateSubject.Text) ? "Неизвестный предмет" : txtConfiscateSubject.Text.Trim();
            confiscate_item(selected.IDSuitcase, _currentUser.IDUser, subject);

            LoadAllSuitcasesForInspector();
            LoadInspectorJournal();
            MessageBox.Show("Предмет конфискован.");
        }

        private void BtnSearchLost_Click(object sender, RoutedEventArgs e)
        {
            LoadLostSuitcases(txtSearchLost.Text.Trim());
        }

        private void BtnUpdateTracking_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgLost.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtCoordinate.Text))
            {
                MessageBox.Show("Введите новую локацию.");
                return;
            }

            update_tracking(selected.IDSuitcase, txtCoordinate.Text.Trim());
            LoadLogisticTimeline();
            MessageBox.Show("Местоположение обновлено.");
        }

        private void BtnAddIncident_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgLost.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан.");
                return;
            }

            Db.Incident.Add(new Incident
            {
                IDSuitcase = selected.IDSuitcase,
                Date = DateTime.Now,
                Place = string.IsNullOrWhiteSpace(txtCoordinate.Text) ? "Логистический центр" : txtCoordinate.Text.Trim(),
                Description = "Логист зафиксировал инцидент по маршруту поиска.",
                IDTypeIncident = 1,
                IDStatysTypeIncident = 1,
                Responsible = _currentUser != null ? (int?)_currentUser.IDUser : null
            });
            Db.SaveChanges();

            LoadPassengerIncidents();
            MessageBox.Show("Инцидент записан.");
        }

        private void lbPassengerSuitcases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentRole == "Passenger")
            {
                BtnTrackPassenger_Click(sender, null);
            }
        }

        private void AddIncidentByRule(string description)
        {
            var lastSuitcaseId = Db.Suitcase.OrderByDescending(s => s.IDSuitcase).Select(s => s.IDSuitcase).FirstOrDefault();
            if (lastSuitcaseId == 0)
            {
                return;
            }

            Db.Incident.Add(new Incident
            {
                IDSuitcase = lastSuitcaseId,
                Date = DateTime.Now,
                Place = "Стойка регистрации",
                Description = description,
                IDTypeIncident = 1,
                IDStatysTypeIncident = 1,
                Responsible = _currentUser != null ? (int?)_currentUser.IDUser : null
            });
            Db.SaveChanges();
        }

        private void LoadPassengerSuitcases()
        {
            if (_currentUser == null)
            {
                lbPassengerSuitcases.ItemsSource = null;
                return;
            }

            var list = Db.Suitcase
                .Where(s => s.Owner == _currentUser.IDUser)
                .Select(s => new SuitcaseGridItem
                {
                    IDSuitcase = s.IDSuitcase,
                    QRKod = s.QRKod,
                    Model = s.Model,
                    Colour = s.Colour,
                    Owner = s.Owner,
                    OwnerName = s.User != null ? s.User.FIO : "Без владельца",
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys != null ? s.Statys.Name : "Без статуса"
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            lbPassengerSuitcases.ItemsSource = list;
        }

        private void LoadPassengerIncidents()
        {
            if (_currentUser == null)
            {
                lbPassengerIncidents.ItemsSource = null;
                return;
            }

            var suitcaseIds = Db.Suitcase.Where(s => s.Owner == _currentUser.IDUser).Select(s => s.IDSuitcase).ToList();
            var incidents = Db.Incident
                .Where(i => i.IDSuitcase.HasValue && suitcaseIds.Contains(i.IDSuitcase.Value))
                .OrderByDescending(i => i.Date)
                .Take(20)
                .ToList()
                .Select(i => string.Format("{0}: {1} | {2}",
                    i.Date.HasValue ? i.Date.Value.ToString("dd.MM.yyyy HH:mm") : "--",
                    string.IsNullOrWhiteSpace(i.Description) ? "Без описания" : i.Description,
                    i.StatysTypeIncident != null ? i.StatysTypeIncident.Name : "Статус не указан"))
                .ToList();

            lbPassengerIncidents.ItemsSource = incidents;
        }

        private void LoadAllSuitcasesForInspector()
        {
            var list = Db.Suitcase
                .Select(s => new SuitcaseGridItem
                {
                    IDSuitcase = s.IDSuitcase,
                    QRKod = s.QRKod,
                    Model = s.Model,
                    Colour = s.Colour,
                    Owner = s.Owner,
                    OwnerName = s.User != null ? s.User.FIO : "Без владельца",
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys != null ? s.Statys.Name : "Без статуса"
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            dgInspector.ItemsSource = list.OrderByDescending(x => x.IDDegreeDanger ?? 0).ThenBy(x => x.StatusName).ToList();
        }

        private void LoadInspectorJournal()
        {
            var logs = Db.Inspection
                .Include(i => i.Suitcase)
                .Include(i => i.User)
                .OrderByDescending(i => i.Date)
                .Take(25)
                .ToList()
                .Select(i => string.Format("{0}: Чемодан #{1} ({2}) — {3}",
                    i.Date.HasValue ? i.Date.Value.ToString("dd.MM HH:mm") : "--",
                    i.IDSuitcase.HasValue ? i.IDSuitcase.Value.ToString() : "?",
                    i.Suitcase != null ? i.Suitcase.QRKod : "без QR",
                    i.Description ?? "досмотр"))
                .ToList();

            lbInspectorJournal.ItemsSource = logs;
        }

        private void LoadLostSuitcases(string qrFilter = "")
        {
            var query = Db.Suitcase.Where(s => s.Statys != null && (s.Statys.Name.Contains("Потер") || s.Statys.Name.Contains("Розыск")));

            if (!string.IsNullOrWhiteSpace(qrFilter))
            {
                query = query.Where(s => s.QRKod.Contains(qrFilter));
            }

            var list = query
                .Select(s => new SuitcaseGridItem
                {
                    IDSuitcase = s.IDSuitcase,
                    QRKod = s.QRKod,
                    Model = s.Model,
                    Colour = s.Colour,
                    Owner = s.Owner,
                    OwnerName = s.User != null ? s.User.FIO : "Без владельца",
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys.Name
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            dgLost.ItemsSource = list;
        }

        private void LoadLogisticTimeline()
        {
            var timeline = Db.Tracking
                .OrderByDescending(t => t.Time)
                .Take(25)
                .ToList()
                .Select(t => string.Format("{0} — Чемодан #{1}: {2}",
                    t.Time.HasValue ? t.Time.Value.ToString("dd.MM HH:mm") : "--",
                    t.IDSuitcase.HasValue ? t.IDSuitcase.Value.ToString() : "?",
                    string.IsNullOrWhiteSpace(t.Coordinate) ? "без локации" : t.Coordinate))
                .ToList();

            lbLogisticTimeline.ItemsSource = timeline;
        }

        private SuitcaseGridItem ResolvePassengerSelection(object sender)
        {
            if (sender is Button btn && btn.Tag is SuitcaseGridItem)
            {
                return (SuitcaseGridItem)btn.Tag;
            }

            return lbPassengerSuitcases.SelectedItem as SuitcaseGridItem;
        }

        private int GetStatusId(string statusName, int fallback = 1)
        {
            var id = Db.Statys.Where(s => s.Name == statusName).Select(s => s.IDStatys).FirstOrDefault();
            return id == 0 ? fallback : id;
        }

        private Brush GetStatusBrush(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return Brushes.Gray;
            if (status.Contains("Потер")) return Brushes.IndianRed;
            if (status.Contains("досмотр") || status.Contains("Досмотр")) return Brushes.DarkOrange;
            if (status.Contains("Конфиск")) return Brushes.DarkViolet;
            if (status.Contains("Зарегистр")) return Brushes.SteelBlue;
            if (status.Contains("пройден") || status.Contains("Прибыл")) return Brushes.SeaGreen;
            return Brushes.SlateGray;
        }

        private string IconForStep(string status)
        {
            var source = status ?? string.Empty;
            if (source.Contains("Зарегистр")) return "🟢";
            if (source.Contains("досмотр") || source.Contains("Досмотр")) return "🟡";
            if (source.Contains("пройден")) return "🔵";
            if (source.Contains("рейс")) return "🟣";
            if (source.Contains("Порт")) return "⚫";
            if (source.Contains("Прибыл")) return "✅";
            return "🔹";
        }

        private class SuitcaseGridItem
        {
            public int IDSuitcase { get; set; }
            public string QRKod { get; set; }
            public string Model { get; set; }
            public string Colour { get; set; }
            public int? Owner { get; set; }
            public string OwnerName { get; set; }
            public int? IDDegreeDanger { get; set; }
            public string StatusName { get; set; }
            public Brush StatusColor { get; set; }
        }
    }
}
