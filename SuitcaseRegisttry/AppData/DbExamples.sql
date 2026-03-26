-- Простые SQL-запросы для курсового проекта "Реестр чемоданов"
-- Работаем с существующей структурой БД SuitcaseRegistry.

-- 1) Вход: найти пользователя по ФИО и роли.
SELECT TOP 1 u.IDUser, u.FIO, r.Name AS RoleName
FROM [User] u
JOIN Roles r ON r.IDRoles = u.IDRoles
WHERE u.FIO = @Fio
  AND r.Name = @RoleName;

-- 2) Пассажир добавляет чемодан (INSERT в Suitcase).
INSERT INTO Suitcase (QRKod, Owner, Model, Colour, Weight, IDDegreeDanger, IDStatys, DateReg, Last_Up, Features)
VALUES (@QRKod, @Owner, @Model, @Colour, @Weight, @IDDegreeDanger, @IDStatys, GETDATE(), GETDATE(), @Features);

-- 3) Пассажир смотрит свои чемоданы (WHERE Owner = user).
SELECT s.IDSuitcase, s.QRKod, s.Model, s.Colour, s.Weight, st.Name AS SuitcaseStatus
FROM Suitcase s
LEFT JOIN Statys st ON st.IDStatys = s.IDStatys
WHERE s.Owner = @Owner
ORDER BY s.DateReg DESC;

-- 4) Инспектор делает досмотр (INSERT в Inspection).
INSERT INTO Inspection (IDSuitcase, Inspector, [Date], IDResult, [Description])
VALUES (@IDSuitcase, @Inspector, GETDATE(), @IDResult, @Description);

-- 5) Инспектор конфискует предмет (INSERT в ConfiscatedItem).
INSERT INTO ConfiscatedItem (IDSuitcase, Inspector, Subject, DateConfiscation)
VALUES (@IDSuitcase, @Inspector, @Subject, GETDATE());

-- 6) Инспектор меняет статус чемодана (UPDATE Suitcase).
UPDATE Suitcase
SET IDStatys = @NewStatus,
    Last_Up = GETDATE()
WHERE IDSuitcase = @IDSuitcase;

-- 7) Логист ищет потерянные чемоданы.
SELECT s.IDSuitcase, s.QRKod, s.Owner, st.Name AS SuitcaseStatus
FROM Suitcase s
JOIN Statys st ON st.IDStatys = s.IDStatys
WHERE st.Name LIKE N'%Потер%';

-- 8) Логист обновляет Tracking.
INSERT INTO Tracking (IDSuitcase, IDFlight, Coordinate, IDStatysTracking, [Time])
VALUES (@IDSuitcase, @IDFlight, @Coordinate, @IDStatysTracking, GETDATE());

-- 9) При проблеме добавляем Incident.
INSERT INTO Incident (IDSuitcase, IDTypeIncident, [Date], [Place], [Description], IDStatysTypeIncident)
VALUES (@IDSuitcase, @IDTypeIncident, GETDATE(), @Place, @Description, @IDStatysTypeIncident);
