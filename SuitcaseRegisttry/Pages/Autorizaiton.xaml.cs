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
                MessageBox.Show("Введите имя.");
                return;
            }

            _currentRole = ((ComboBoxItem)cbRole.SelectedItem).Content.ToString();
            _currentUser = GetOrCreateUser(fio, _currentRole);
            txtCurrentUser.Text = $"Вошёл: {_currentUser.FIO} ({_currentRole})";

            PassengerPanel.Visibility = _currentRole == "Passenger" ? Visibility.Visible : Visibility.Collapsed;
            InspectorPanel.Visibility = _currentRole == "Inspector" ? Visibility.Visible : Visibility.Collapsed;
            LogisticPanel.Visibility = _currentRole == "Logistician" ? Visibility.Visible : Visibility.Collapsed;

            LoadPassengerSuitcases();
            LoadAllSuitcasesForInspector();
            LoadLostSuitcases();
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
                Token = Guid.NewGuid().ToString("N")
            };
            Db.User.Add(user);
            Db.SaveChanges();
            return user;
        }

        // Функция из задания: add_suitcase
        private void add_suitcase(int ownerId, string model, string colour, int weight, int dangerDegree)
        {
            var statusId = GetStatusId("Зарегистрирован");

            if (dangerDegree > 2)
            {
                statusId = GetStatusId("На досмотре", statusId);
            }

            var sql = @"INSERT INTO Suitcase
                        (QRKod, Owner, Model, Colour, Weight, IDDegreeDanger, IDStatys, DateReg, Last_Up, Features)
                        VALUES
                        (@qr, @owner, @model, @colour, @weight, @danger, @status, GETDATE(), GETDATE(), @features)";

            Db.Database.ExecuteSqlCommand(
                sql,
                new SqlParameter("@qr", "QR-" + DateTime.Now.ToString("yyyyMMddHHmmss")),
                new SqlParameter("@owner", ownerId),
                new SqlParameter("@model", model),
                new SqlParameter("@colour", colour),
                new SqlParameter("@weight", weight),
                new SqlParameter("@danger", dangerDegree),
                new SqlParameter("@status", statusId),
                new SqlParameter("@features", dangerDegree > 2 ? "Авто: направлен на досмотр" : "Обычная регистрация")
            );
        }

        // Функция из задания: inspect_suitcase
        private void inspect_suitcase(int suitcaseId, int inspectorId)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Inspection (IDSuitcase, Inspector, Date, IDResult, Description)
                  VALUES (@sid, @insp, GETDATE(), @result, @desc)",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@insp", inspectorId),
                new SqlParameter("@result", 1),
                new SqlParameter("@desc", "Плановый досмотр")
            );

            Db.Database.ExecuteSqlCommand(
                @"UPDATE Suitcase SET IDStatys = @status, Last_Up = GETDATE() WHERE IDSuitcase = @sid",
                new SqlParameter("@status", GetStatusId("На досмотре")),
                new SqlParameter("@sid", suitcaseId)
            );
        }

        // Функция из задания: confiscate_item
        private void confiscate_item(int suitcaseId, int inspectorId, string subject)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO ConfiscatedItem (IDSuitcase, Inspector, Subject, DateConfiscation)
                  VALUES (@sid, @insp, @subject, GETDATE())",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@insp", inspectorId),
                new SqlParameter("@subject", subject)
            );

            Db.Database.ExecuteSqlCommand(
                @"UPDATE Suitcase SET IDStatys = @status, Last_Up = GETDATE() WHERE IDSuitcase = @sid",
                new SqlParameter("@status", GetStatusId("Конфискован", GetStatusId("На досмотре"))),
                new SqlParameter("@sid", suitcaseId)
            );
        }

        // Функция из задания: update_tracking
        private void update_tracking(int suitcaseId, string coordinate)
        {
            Db.Database.ExecuteSqlCommand(
                @"INSERT INTO Tracking (IDSuitcase, Coordinate, IDStatysTracking, Time)
                  VALUES (@sid, @coord, @status, GETDATE())",
                new SqlParameter("@sid", suitcaseId),
                new SqlParameter("@coord", coordinate),
                new SqlParameter("@status", 1)
            );
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
                AddIncidentByRule("Опасность выше 2. Отправлен на досмотр.");
            }

            LoadPassengerSuitcases();
            LoadAllSuitcasesForInspector();
            MessageBox.Show("Чемодан добавлен.");
        }

        private void BtnTrackPassenger_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgPassenger.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите чемодан.");
                return;
            }

            var history = Db.Tracking
                .Where(t => t.IDSuitcase == selected.IDSuitcase)
                .OrderByDescending(t => t.Time)
                .Take(20)
                .Select(t => (t.Time.HasValue ? t.Time.Value.ToString("dd.MM HH:mm") : "--") + " | " + t.Coordinate)
                .ToList();

            lbPassengerTrack.ItemsSource = history;
        }

        private void BtnInspect_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null) return;
            var selected = dgInspector.SelectedItem as SuitcaseGridItem;
            if (selected == null) return;

            inspect_suitcase(selected.IDSuitcase, _currentUser.IDUser);
            LoadAllSuitcasesForInspector();
        }

        private void BtnConfiscate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null) return;
            var selected = dgInspector.SelectedItem as SuitcaseGridItem;
            if (selected == null) return;

            var subject = string.IsNullOrWhiteSpace(txtConfiscateSubject.Text) ? "Неизвестный предмет" : txtConfiscateSubject.Text.Trim();
            confiscate_item(selected.IDSuitcase, _currentUser.IDUser, subject);
            LoadAllSuitcasesForInspector();
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
                return;
            }

            update_tracking(selected.IDSuitcase, txtCoordinate.Text.Trim());
            MessageBox.Show("Координата обновлена.");
        }

        private void BtnAddIncident_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgLost.SelectedItem as SuitcaseGridItem;
            if (selected == null)
            {
                return;
            }

            Db.Incident.Add(new Incident
            {
                IDSuitcase = selected.IDSuitcase,
                Date = DateTime.Now,
                Place = txtCoordinate.Text,
                Description = "Проблема с потерянным чемоданом",
                IDTypeIncident = 1,
                IDStatysTypeIncident = 1,
                Responsible = _currentUser?.IDUser
            });
            Db.SaveChanges();
            MessageBox.Show("Incident записан.");
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
                Responsible = _currentUser?.IDUser
            });
            Db.SaveChanges();
        }

        private void LoadPassengerSuitcases()
        {
            if (_currentUser == null)
            {
                return;
            }

            // SELECT из задания: где Owner = пользователь.
            var list = Db.Suitcase
                .Where(s => s.Owner == _currentUser.IDUser)
                .Select(s => new SuitcaseGridItem
                {
                    IDSuitcase = s.IDSuitcase,
                    QRKod = s.QRKod,
                    Model = s.Model,
                    Owner = s.Owner,
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys != null ? s.Statys.Name : "Без статуса"
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            dgPassenger.ItemsSource = list;
        }

        private void LoadAllSuitcasesForInspector()
        {
            var list = Db.Suitcase
                .Select(s => new SuitcaseGridItem
                {
                    IDSuitcase = s.IDSuitcase,
                    QRKod = s.QRKod,
                    Model = s.Model,
                    Owner = s.Owner,
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys != null ? s.Statys.Name : "Без статуса"
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            dgInspector.ItemsSource = list;
        }

        private void LoadLostSuitcases(string qrFilter = "")
        {
            var query = Db.Suitcase.Where(s => s.Statys != null && s.Statys.Name.Contains("Потер"));

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
                    Owner = s.Owner,
                    IDDegreeDanger = s.IDDegreeDanger,
                    StatusName = s.Statys.Name
                })
                .ToList();

            list.ForEach(x => x.StatusColor = GetStatusBrush(x.StatusName));
            dgLost.ItemsSource = list;
        }

        private int GetStatusId(string statusName, int fallback = 1)
        {
            var id = Db.Statys.Where(s => s.Name == statusName).Select(s => s.IDStatys).FirstOrDefault();
            return id == 0 ? fallback : id;
        }

        private Brush GetStatusBrush(string status)
        {
            if (status == null) return Brushes.Gray;
            if (status.Contains("Потер")) return Brushes.IndianRed;
            if (status.Contains("досмотр") || status.Contains("Досмотр")) return Brushes.Orange;
            if (status.Contains("Зарегистр")) return Brushes.SteelBlue;
            return Brushes.Gray;
        }

        private class SuitcaseGridItem
        {
            public int IDSuitcase { get; set; }
            public string QRKod { get; set; }
            public string Model { get; set; }
            public int? Owner { get; set; }
            public int? IDDegreeDanger { get; set; }
            public string StatusName { get; set; }
            public Brush StatusColor { get; set; }
        }
    }
}
