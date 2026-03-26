-- Примеры простых запросов после обновления структуры.

-- 1) Список чемоданов пассажира + статус + рейс.
SELECT s.IDSuitcase,
       s.QRKod,
       st.Name AS SuitcaseStatus,
       f.DestinationPortal,
       f.DateTime
FROM Suitcase s
LEFT JOIN Statys st ON st.IDStatys = s.IDStatys
LEFT JOIN Flight f ON f.IDFlight = s.CurrentFlightId
ORDER BY s.DateReg DESC;

-- 2) Если чемодан опасный, отправляем на досмотр.
UPDATE Suitcase
SET IDStatys = (SELECT TOP 1 IDStatys FROM Statys WHERE Name = N'На досмотре'),
    Last_Up = GETDATE()
WHERE IDSuitcase = @SuitcaseId
  AND (SignDanger = N'Опасно' OR Weight > 30);

-- 3) Если произошёл побег, создаём INCIDENT.
INSERT INTO Incident (IDSuitcase, IDFlight, [Date], [Place], [Description], IDStatysTypeIncident, IncidentCode)
VALUES (@SuitcaseId, @FlightId, GETDATE(), N'Зона контроля', N'Побег чемодана', 1, N'ESCAPE');

-- 4) Запись в простой трекинг-таймлайн.
INSERT INTO Tracking (IDSuitcase, IDFlight, [Time], EventText)
VALUES (@SuitcaseId, @FlightId, GETDATE(), N'Чемодан передан инспектору на досмотр');
