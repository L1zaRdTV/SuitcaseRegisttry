-- Минимальное и безопасное улучшение БД (без ломки структуры)
-- Разрешено в задании: добавить только 1 поле при необходимости.

-- Добавим поле для короткой пометки логиста в Tracking.
IF COL_LENGTH('Tracking', 'LogisticComment') IS NULL
BEGIN
    ALTER TABLE Tracking ADD LogisticComment NVARCHAR(150) NULL;
END;

-- Ничего не удаляем и не меняем существующие FK/таблицы.
