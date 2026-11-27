-- ==============================================================
-- Резервное копирование базы данных VelosipedDriveDB
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
BACKUP DATABASE VelosipedDriveDB
TO DISK = N'C:\Backup\user<Номер рабочего места>.bak'
WITH FORMAT, COMPRESSION, CHECKSUM,
     NAME = N'VelosipedDriveDB-full';
GO
-- Проверка: пробное восстановление в отдельную базу
RESTORE VERIFYONLY FROM DISK = N'C:\Backup\user<Номер рабочего места>.bak';
GO
RESTORE DATABASE VelosipedDriveDB_Check
FROM DISK = N'C:\Backup\user<Номер рабочего места>.bak'
WITH MOVE N'VelosipedDriveDB' TO N'C:\Backup\VelosipedDriveDB_Check.mdf',
     MOVE N'VelosipedDriveDB_log' TO N'C:\Backup\VelosipedDriveDB_Check.ldf',
     REPLACE;
GO
SELECT COUNT(*) AS [ProductRows] FROM VelosipedDriveDB_Check.dbo.Product;
GO
