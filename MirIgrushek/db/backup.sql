-- ==============================================================
-- Резервное копирование базы данных MirIgrushekDB
-- Вариативная часть ДЭ: файл копии «user<Номер рабочего места>.bak»
-- Выполняется в SSMS (xp_cmdshell требует прав sysadmin).
-- ==============================================================
USE master;
GO
EXEC sp_configure 'xp_cmdshell', 1;
RECONFIGURE;
GO
EXEC xp_cmdshell 'IF NOT EXIST "C:\Backup" MD "C:\Backup"';
GO
BACKUP DATABASE MirIgrushekDB
TO DISK = N'C:\Backup\user<Номер рабочего места>.bak'
WITH FORMAT, COMPRESSION, CHECKSUM,
     NAME = N'MirIgrushekDB-full';
GO
-- Проверка: пробное восстановление в отдельную базу
RESTORE VERIFYONLY FROM DISK = N'C:\Backup\user<Номер рабочего места>.bak';
GO
RESTORE DATABASE MirIgrushekDB_Check
FROM DISK = N'C:\Backup\user<Номер рабочего места>.bak'
WITH MOVE N'MirIgrushekDB' TO N'C:\Backup\MirIgrushekDB_Check.mdf',
     MOVE N'MirIgrushekDB_log' TO N'C:\Backup\MirIgrushekDB_Check.ldf',
     REPLACE;
GO
SELECT COUNT(*) AS [ProductRows] FROM MirIgrushekDB_Check.dbo.Product;
GO
