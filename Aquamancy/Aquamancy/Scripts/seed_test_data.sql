-- Script SQL pour ajouter des données de test dans Aquamancy
-- Ce script crée 3 sondes et génère des données de température et TDS sur 24h

-- Nettoyer les données existantes (optionnel, décommenter si nécessaire)
-- DELETE FROM temperature_readings;
-- DELETE FROM tds_readings;
-- DELETE FROM probes;

-- Créer 3 sondes de test
INSERT INTO probes (name, machine_name, color, min_temperature, max_temperature, send_frequency_in_seconds, created_at, last_communication_date, last_booted_at, rssi)
VALUES 
	('Aquarium Principal', 'probe-1', '#3498db', 24.0, 26.0, 300, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(NOW(), INTERVAL 5 MINUTE), DATE_SUB(NOW(), INTERVAL 7 DAY), -65),
	('Aquarium Récifal', 'probe-2', '#e74c3c', 25.0, 27.0, 300, DATE_SUB(NOW(), INTERVAL 30 DAY), DATE_SUB(NOW(), INTERVAL 3 MINUTE), DATE_SUB(NOW(), INTERVAL 5 DAY), -55),
	('Bassin Quarantaine', 'probe-3', '#2ecc71', 23.0, 25.0, 300, DATE_SUB(NOW(), INTERVAL 15 DAY), DATE_SUB(NOW(), INTERVAL 10 MINUTE), DATE_SUB(NOW(), INTERVAL 3 DAY), -72);

-- Récupérer les IDs des sondes créées
SET @probe1_id = (SELECT id FROM probes WHERE machine_name = 'probe-1');
SET @probe2_id = (SELECT id FROM probes WHERE machine_name = 'probe-2');
SET @probe3_id = (SELECT id FROM probes WHERE machine_name = 'probe-3');

-- Générer des données de température pour les dernières 24h (toutes les 5 minutes = 288 points par sonde)
-- Aquarium Principal (température moyenne 25°C, plage 24-26°C)
INSERT INTO temperature_readings (probe_id, timestamp, temperature)
SELECT 
	@probe1_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(25 + SIN((n * 5) * PI() / 360) * 0.5 + (RAND() - 0.5) * 0.4, 2) as temperature
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Aquarium Récifal (température moyenne 26°C, plage 25-27°C)
INSERT INTO temperature_readings (probe_id, timestamp, temperature)
SELECT 
	@probe2_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(26 + SIN((n * 5) * PI() / 360) * 0.5 + (RAND() - 0.5) * 0.4, 2) as temperature
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Bassin Quarantaine (température moyenne 24°C, plage 23-25°C)
INSERT INTO temperature_readings (probe_id, timestamp, temperature)
SELECT 
	@probe3_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(24 + SIN((n * 5) * PI() / 360) * 0.5 + (RAND() - 0.5) * 0.4, 2) as temperature
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Générer des données de TDS pour les dernières 24h
-- Aquarium Principal (TDS moyen 250 ppm)
INSERT INTO tds_readings (probe_id, timestamp, tds)
SELECT 
	@probe1_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(250 + (RAND() - 0.5) * 40, 2) as tds
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Aquarium Récifal (TDS moyen 180 ppm)
INSERT INTO tds_readings (probe_id, timestamp, tds)
SELECT 
	@probe2_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(180 + (RAND() - 0.5) * 30, 2) as tds
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Bassin Quarantaine (TDS moyen 320 ppm)
INSERT INTO tds_readings (probe_id, timestamp, tds)
SELECT 
	@probe3_id,
	DATE_SUB(NOW(), INTERVAL (1440 - (n * 5)) MINUTE) as timestamp,
	ROUND(320 + (RAND() - 0.5) * 50, 2) as tds
FROM (
	SELECT a.N + b.N * 10 + c.N * 100 as n
	FROM 
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) a,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2 UNION SELECT 3 UNION SELECT 4 UNION SELECT 5 UNION SELECT 6 UNION SELECT 7 UNION SELECT 8 UNION SELECT 9) b,
		(SELECT 0 AS N UNION SELECT 1 UNION SELECT 2) c
) numbers
WHERE n < 288;

-- Vérifier les données créées
SELECT 'Sondes créées' as Info, COUNT(*) as Count FROM probes
UNION ALL
SELECT 'Lectures de température' as Info, COUNT(*) as Count FROM temperature_readings
UNION ALL
SELECT 'Lectures de TDS' as Info, COUNT(*) as Count FROM tds_readings;
