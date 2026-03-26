-- Минимальное обновление БД для проекта "Реестр чемоданов"
-- Цель: поправить статусы, связь чемодан ↔ рейс, инциденты и трекинг.

-- 1) Статусы чемодана: добавим недостающий статус "На досмотре" если его нет.
IF NOT EXISTS (SELECT 1 FROM Statys WHERE Name = N'На досмотре')
BEGIN
    INSERT INTO Statys (Name) VALUES (N'На досмотре');
END;

-- 2) Прямая связь чемодан → текущий рейс.
IF COL_LENGTH('Suitcase', 'CurrentFlightId') IS NULL
BEGIN
    ALTER TABLE Suitcase ADD CurrentFlightId INT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_Suitcase_CurrentFlight'
)
BEGIN
    ALTER TABLE Suitcase
    ADD CONSTRAINT FK_Suitcase_CurrentFlight
    FOREIGN KEY (CurrentFlightId) REFERENCES Flight(IDFlight);
END;

-- 3) Инциденты: добавим код инцидента для быстрых проверок (например ESCAPE).
IF COL_LENGTH('Incident', 'IncidentCode') IS NULL
BEGIN
    ALTER TABLE Incident ADD IncidentCode NVARCHAR(20) NULL;
END;

-- 4) Трекинг: добавим текст события, чтобы хранить простой таймлайн.
IF COL_LENGTH('Tracking', 'EventText') IS NULL
BEGIN
    ALTER TABLE Tracking ADD EventText NVARCHAR(200) NULL;
END;

-- 5) Небольшая чистка: индекс на поиск трекинга чемодана по времени.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Tracking_Suitcase_Time'
)
BEGIN
    CREATE INDEX IX_Tracking_Suitcase_Time ON Tracking (IDSuitcase, [Time]);
END;
