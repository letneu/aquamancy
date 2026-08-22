import network
import urequests
import machine
import rp2
import socket
import select
from machine import ADC, Pin
import ds18x20
import time
import onewire
import json
import os
# Voir github.com/letneu/aquamancy/wiki

# Codes d'erreur (nombre de clignotement de la led) :
# 3 : Erreur de connexion au wifi
# 5 : Erreur de communication avec le serveur
# 6 : Erreur de récupération de l'ID unique de la machine
# 7 : Erreur de lecture du fichier de configuration


print("Lancement de aquamancy")

# Sur les anciens modèles c'est 28 et 27 pour les nouveaux
TEMPERATURE_DATA_PIN = 17

# Broche de la sonde TDS
TDS_PIN = 28
VREF = 3.3
ADC_RESOLUTION = 65535
TDS_SAMPLES = 30
TDS_MIN_VALID_PPM = 10
# Ecart de tension (pull-up vs pull-down) au-dela duquel la broche est consideree flottante
TDS_DETECT_DELTA_V = 2.0

# --- Configuration des broches pour la led ---
LED_PIN_R = 21
LED_PIN_G = 20
LED_PIN_B = 19

FREQ = 1000  # frequence PWM en Hz
DEFAULT_LED_COLOR = (255, 255, 255)

# --- Configuration du mode appairage (portail captif) ---
AP_SSID = "Sonde-Aquamancy-Setup"
AP_IP = "192.168.4.1"

# Messages correspondant aux codes d'erreur
ERROR_MESSAGES = {
    3: "Erreur de connexion au wifi",
    5: "Erreur de communication avec le serveur",
    6: "Erreur de récupération de l'ID unique de la machine",
    7: "Erreur de lecture du fichier de configuration",
}

# -------------------------
# FONCTIONS
# -------------------------

# Fonction de lecture de la tension de la sonde TDS
def read_tds_voltage():
    raw = tds_adc.read_u16()
    return raw * VREF / ADC_RESOLUTION

# Detection de la presence de la sonde TDS : une broche flottante suit les
# resistances de tirage internes, alors que la sonde impose sa tension de sortie.
def tds_probe_present():
    global tds_adc
    try:
        Pin(TDS_PIN, Pin.IN, Pin.PULL_DOWN)
        time.sleep_ms(5)
        v_down = tds_adc.read_u16() * VREF / ADC_RESOLUTION
        Pin(TDS_PIN, Pin.IN, Pin.PULL_UP)
        time.sleep_ms(5)
        v_up = tds_adc.read_u16() * VREF / ADC_RESOLUTION
    finally:
        # Retour en mode ADC pur (sans resistance de tirage)
        tds_adc = ADC(Pin(TDS_PIN))
    return (v_up - v_down) < TDS_DETECT_DELTA_V

# Fonction de conversion tension -> TDS en ppm (formule DFRobot)
def voltage_to_tds(voltage, temperature=25.0):
    compensation_coefficient = 1.0 + 0.02 * (temperature - 25.0)
    compensated_voltage = voltage / compensation_coefficient
    tds_value = (133.42 * compensated_voltage**3
                 - 255.86 * compensated_voltage**2
                 + 857.39 * compensated_voltage) * 0.5
    return tds_value

# Fonction de lecture du TDS (moyenne sur TDS_SAMPLES mesures)
def read_tds_ppm(temperature=25.0):
    if not tds_probe_present():
        raise Exception("Sonde TDS absente (broche flottante)")
    samples = [read_tds_voltage() for _ in range(TDS_SAMPLES)]
    avg_voltage = sum(samples) / len(samples)
    tds_ppm = round(voltage_to_tds(avg_voltage, temperature))
    return tds_ppm, avg_voltage

# Vérification de la validité d'une mesure TDS
def validate_tds(tds_ppm):
    if tds_ppm <= TDS_MIN_VALID_PPM:
        raise Exception("TDS invalide : {:.0f} ppm".format(tds_ppm))

# Fonction de récupération de l'ID unique de la machine
def get_unique_id():
    while True:
        try:
            # Id unique mais un peu long et moche
            uid = machine.unique_id().hex()
            print("ID machine :", uid)
            return uid
        except Exception as e:
            # Erreur de récupération de l'ID unique de la machine, code 6
            error_blink(6, 60)
            print("Erreur de récupération de l'ID unique de la machine", e)

# Fonction de connexion à la sonde de température
def temperature_probe_connect():
    global ds, rom
    try:
        # Connexion à la sonde sur le GPIO
        datapin = machine.Pin(TEMPERATURE_DATA_PIN)
        ow = onewire.OneWire(datapin)
        ds = ds18x20.DS18X20(ow)

        # On est en onewire mais on a besoin de gérer qu'un seul truc (la sonde))
        roms = ds.scan()
        if not roms:
            raise Exception("Aucune sonde détectée")
        rom = roms[0]

        # Vérification de la valeur
        ds.convert_temp()
        time.sleep_ms(750)
        temp = ds.read_temp(rom)
        if temp <= 0:
            raise Exception("Température invalide : {}".format(temp))
    except Exception as e:
        print("Erreur de connexion à la sonde de température :", e)
        return False
    return True

# Fonction de vérification de la sonde TDS
def tds_probe_connect():
    try:
        tds_ppm, _ = read_tds_ppm()
        validate_tds(tds_ppm)
    except Exception as e:
        print("Erreur de connexion à la sonde TDS :", e)
        return False
    return True

# Fonction de connexion au réseau Wi-Fi
def wifi_connect():
    global wlan, server_url
    try:
        # Creation du fichier de config si n'existe pas
        if "config.json" not in os.listdir():
            with open("config.json", "w") as f:
                json.dump({
                "wifi_ssid": "",
                "wifi_password": "",
                "server_base_url": "",
                "server_submit_url" : "/api/submit"
            }, f)

        # Miam miam le fichier
        with open("config.json") as f:
            config = json.load(f)

        # Setup des variables
        wifi_ssid = config["wifi_ssid"]
        wifi_password = config["wifi_password"]
        server_base_url = config.get("server_base_url")
        server_submit_url = config.get("server_submit_url")
        server_url = server_base_url + server_submit_url
    except Exception as e:
        # Erreur de récupération du fichier de config, code 7
        print("Erreur de lecture du fichier de configuration :", e)
        # Fichier corrompu : on le supprime pour qu'il soit recréé au prochain démarrage
        if "config.json" in os.listdir():
            os.remove("config.json")
        error_blink(7, 60)
        return False

    try:
        wlan = network.WLAN(network.STA_IF)
        wlan.active(True)
        wlan.connect(wifi_ssid, wifi_password)

        print("Connexion Wi-Fi… ({} / {})".format(wifi_ssid, wifi_password))

        for y in range(10):
            time.sleep(2)
            if wlan.isconnected():
                break
     
        if not wlan.isconnected():
            raise Exception("Échec de la connexion Wi-Fi")

        print("Connecté :", wlan.ifconfig())
    except Exception as e:
        print("Erreur de connexion au wifi :", e)
        # Erreur de connexion au wifi, code 3
        error_blink(3, 60)
        return False
    return True

# Convertit une valeur 0-255 en duty 0-65535
def led_duty(value):
    return int((value / 255) * 65535)

# Set de la couleur de la led
def led_set_color(r, g, b):
    # duty_u16 attend une valeur entre 0 et 65535
    pwm_r.duty_u16(led_duty(r))
    pwm_g.duty_u16(led_duty(g))
    pwm_b.duty_u16(led_duty(b))

# Extinction de la led
def led_turn_off():
    led_set_color(0, 0, 0)

# Verif des valeurs RGB
def clamp_color_value(value, default):
    try:
        value = int(value)
    except (TypeError, ValueError):
        print("clamp_color_value - valeur invalide ({!r}), utilisation du défaut {}".format(value, default))
        return default

    if value < 0:
        print("clamp_color_value - valeur {} < 0, ramenée à 0".format(value))
        return 0
    if value > 255:
        print("clamp_color_value - valeur {} > 255, ramenée à 255".format(value))
        return 255
    return value

# Parse de la réponse du serveur pour la led
def get_response_led_color(response_data):
    default_r, default_g, default_b = DEFAULT_LED_COLOR

    raw_r = response_data.get("colorR")
    raw_g = response_data.get("colorG")
    raw_b = response_data.get("colorB")
    print("get_response_led_color - valeurs brutes reçues :", raw_r, raw_g, raw_b)

    color = (
        clamp_color_value(raw_r, default_r),
        clamp_color_value(raw_g, default_g),
        clamp_color_value(raw_b, default_b),
    )
    print("get_response_led_color - couleur calculée (R, G, B) :", color)

    return color


# Fonction de clignotement en cas d'erreur
def error_blink(blink_count, duration):
    global last_error_code
    last_error_code = blink_count

    blink_duration = 0.5
    pause_duration = 4

    cycle_duration = (blink_duration * 2) * blink_count + pause_duration
    cycle_count = int(duration / cycle_duration)

    # On clignote un certain nombre de fois puis dodo pendant [pause_duration]
    # (avec surveillance du bouton BOOTSEL pour le mode appairage)
    for y in range(cycle_count):
        for i in range(blink_count):
            led_set_color(255, 255, 255)
            sleep_with_pairing_check(blink_duration)
            led_turn_off()
            sleep_with_pairing_check(blink_duration)
        sleep_with_pairing_check(pause_duration)

# -------------------------
# MODE APPAIRAGE (portail captif)
# Inspire de github.com/CodyTolene/Pico-Portal et github.com/cfreshman/pico-fi
# -------------------------

# Lecture de la config avec valeurs par defaut
def pairing_load_config():
    config = dict({
    "wifi_ssid": "",
    "wifi_password": "",
    "server_base_url": "",
    "server_submit_url": "/api/submit",
})
    try:
        if "config.json" in os.listdir():
            with open("config.json") as f:
                config.update(json.load(f))
    except Exception as e:
        print("Appairage - config illisible :", e)
    return config


# Sauvegarde de la config
def pairing_save_config(config):
    with open("config.json", "w") as f:
        json.dump(config, f)


# Decodage des caracteres encodes dans l'URL (%xx et +)
def url_decode(s):
    s = s.replace("+", " ")
    out = ""
    i = 0
    while i < len(s):
        if s[i] == "%" and i + 2 < len(s) + 1:
            try:
                out += chr(int(s[i + 1:i + 3], 16))
                i += 3
                continue
            except ValueError:
                pass
        out += s[i]
        i += 1
    return out


# Parse du corps d'un formulaire urlencoded
def parse_form(body):
    fields = {}
    for pair in body.split("&"):
        if "=" in pair:
            k, v = pair.split("=", 1)
            fields[url_decode(k)] = url_decode(v)
    return fields


# Echappement HTML
def html_escape(s):
    return (str(s).replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))


# Bloc HTML de statut de la derniere boucle d'envoi
def build_status_block():
    if last_error_code is None:
        return ('<p style="background:#1d5c33;padding:10px;border-radius:6px;'
                'text-align:center">Configuration OK</p>')
    message = ERROR_MESSAGES.get(last_error_code, "Erreur inconnue")
    return ('<p style="background:#8a2b2b;padding:10px;border-radius:6px;'
            'text-align:center">Erreur {code} : {message}</p>').format(
        code=last_error_code, message=html_escape(message))


# Page HTML du formulaire de configuration
def build_form_page(config):
    return """<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Configuration Aquamancy</title>
<style>
body{{font-family:sans-serif;background:#0e2a3a;color:#eee;margin:0;padding:20px}}
.card{{max-width:420px;margin:auto;background:#16394f;border-radius:12px;padding:24px}}
h1{{font-size:1.3em;text-align:center}}
label{{display:block;margin-top:14px;font-size:.9em}}
input{{width:100%;padding:10px;margin-top:4px;border-radius:6px;border:none;box-sizing:border-box}}
button{{width:100%;margin-top:20px;padding:12px;border:none;border-radius:6px;background:#2fa8d5;color:#fff;font-size:1em}}
</style>
</head>
<body>
<div class="card">
<h1>Configuration Aquamancy</h1>
{status_block}
<form method="POST" action="/save">
<label>SSID Wi-Fi
<input name="wifi_ssid" value="{wifi_ssid}" required></label>
<label>Mot de passe Wi-Fi
<input name="wifi_password" value="{wifi_password}"></label>
<label>URL du serveur
<input name="server_base_url" value="{server_base_url}" placeholder="http://exemple.com"></label>
<label>Chemin d'envoi
<input name="server_submit_url" value="{server_submit_url}"></label>
<button type="submit">Enregistrer et red&eacute;marrer</button>
</form>
</div>
</body>
</html>""".format(
        status_block=build_status_block(),
        wifi_ssid=html_escape(config.get("wifi_ssid", "")),
        wifi_password=html_escape(config.get("wifi_password", "")),
        server_base_url=html_escape(config.get("server_base_url", "")),
        server_submit_url=html_escape(config.get("server_submit_url", "/api/submit")),
    )


SUCCESS_PAGE = """<!DOCTYPE html>
<html lang="fr">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Enregistr&eacute;</title>
<style>body{font-family:sans-serif;background:#0e2a3a;color:#eee;text-align:center;padding:40px}</style>
</head>
<body><h1>Configuration enregistr&eacute;e</h1>
<p>L'appareil red&eacute;marre&hellip; Vous pouvez fermer cette page.</p>
</body></html>"""


# Demarrage du point d'acces WiFi
def start_ap():
    # Coupe le mode station pour eviter les conflits
    sta = network.WLAN(network.STA_IF)
    sta.active(False)

    ap = network.WLAN(network.AP_IF)
    ap.config(essid=AP_SSID, security=0)
    ap.active(True)
    while not ap.active():
        time.sleep(0.1)
    # ifconfig apres activation (certaines versions de MicroPython l'ignorent avant)
    try:
        ap.ifconfig((AP_IP, "255.255.255.0", AP_IP, AP_IP))
    except Exception as e:
        print("Appairage - ifconfig AP :", e)
    print("Appairage - AP actif :", ap.ifconfig())
    return ap


# Reponse DNS minimale : les requetes de type A pointent vers l'IP du portail.
# Les autres types (AAAA, HTTPS...) recoivent une reponse vide (sans erreur),
# sinon Android rejette la reponse et reessaie en boucle.
def dns_response(query, ip):
    try:
        if len(query) < 12:
            return None
        # Fin du nom de la question
        i = 12
        while i < len(query) and query[i] != 0:
            i += query[i] + 1
        if i + 5 > len(query):
            return None
        qtype = (query[i + 1] << 8) | query[i + 2]
        question = query[12:i + 5]
        header = (
            query[:2]              # ID de transaction
            + b"\x81\x80"          # Flags : reponse, recursion disponible
            + b"\x00\x01"          # QDCOUNT
        )
        if qtype == 1:  # Type A : on repond avec l'IP du portail
            return (header
                    + b"\x00\x01"          # ANCOUNT = 1
                    + b"\x00\x00\x00\x00"  # NSCOUNT / ARCOUNT
                    + question
                    + b"\xc0\x0c"          # Pointeur vers le nom
                    + b"\x00\x01\x00\x01"  # Type A, classe IN
                    + b"\x00\x00\x00\x3c"  # TTL 60 s
                    + b"\x00\x04"          # 4 octets
                    + bytes(int(x) for x in ip.split(".")))
        # Autres types : reponse vide (pas d'enregistrement, pas d'erreur)
        return (header
                + b"\x00\x00"          # ANCOUNT = 0
                + b"\x00\x00\x00\x00"  # NSCOUNT / ARCOUNT
                + question)
    except Exception as e:
        print("Appairage - erreur dns_response :", e)
        return None


# Envoi d'une reponse HTTP
def send_response(conn, status, body, content_type="text/html", extra_headers=""):
    if isinstance(body, str):
        body = body.encode()
    header = ("HTTP/1.1 {}\r\nContent-Type: {}\r\nContent-Length: {}\r\n"
              "Cache-Control: no-store\r\nConnection: close\r\n{}\r\n").format(
        status, content_type, len(body), extra_headers)
    conn.sendall(header.encode() + body)


# Redirection vers le portail (declenche la detection captive sur Android)
def redirect_to_portal(conn):
    send_response(conn, "302 Found", "",
                  extra_headers="Location: http://{}/\r\n".format(AP_IP))


# Traitement d'une requete HTTP, retourne True si la config a ete enregistree
def handle_http(conn):
    conn.settimeout(5)
    try:
        request = conn.recv(2048)
        if not request:
            return False
        request = request.decode("utf-8", "ignore")
        line = request.split("\r\n", 1)[0]
        parts = line.split(" ")
        if len(parts) < 2:
            return False
        method, path = parts[0], parts[1]
        print("Appairage - {} {}".format(method, path))

        if method == "POST" and path.startswith("/save"):
            body = request.split("\r\n\r\n", 1)[1] if "\r\n\r\n" in request else ""
            # Lit le reste du corps si necessaire
            cl = 0
            for h in request.split("\r\n"):
                if h.lower().startswith("content-length:"):
                    cl = int(h.split(":", 1)[1].strip())
            while len(body) < cl:
                more = conn.recv(1024)
                if not more:
                    break
                body += more.decode("utf-8", "ignore")

            fields = parse_form(body)
            config = pairing_load_config()
            for key in ("wifi_ssid", "wifi_password", "server_base_url", "server_submit_url"):
                if key in fields:
                    config[key] = fields[key]
            pairing_save_config(config)
            print("Appairage - config enregistree :", config)
            send_response(conn, "200 OK", SUCCESS_PAGE)
            return True

        if method == "GET" and (path == "/" or path.startswith("/index")):
            send_response(conn, "200 OK", build_form_page(pairing_load_config()))
            return False

        # Detection de portail captif (Android : /generate_204, autres OS)
        redirect_to_portal(conn)
        return False
    except Exception as e:
        print("Appairage - erreur HTTP :", e)
        return False
    finally:
        conn.close()


# Lance le mode appairage. Ne retourne jamais : redemarre apres enregistrement.
def pairing_mode():
    print("Appairage - demarrage du mode appairage")

    start_ap()

    dns_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dns_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    dns_sock.bind(("0.0.0.0", 53))

    http_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    http_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    http_sock.bind(("0.0.0.0", 80))
    http_sock.listen(4)

    poller = select.poll()
    poller.register(dns_sock, select.POLLIN)
    poller.register(http_sock, select.POLLIN)

    # Clignotement blanc continu pour signaler le mode appairage
    blink_interval_ms = 500
    led_on = False
    last_blink_ms = time.ticks_ms()
    led_set_color(255, 255, 255)
    led_on = True

    saved = False
    while not saved:
        # Fait clignoter la led en blanc pendant l'attente
        now = time.ticks_ms()
        if time.ticks_diff(now, last_blink_ms) >= blink_interval_ms:
            led_on = not led_on
            led_set_color(255, 255, 255) if led_on else led_turn_off()
            last_blink_ms = now

        for sock, event in poller.poll(blink_interval_ms):
            if sock is dns_sock:
                try:
                    query, addr = dns_sock.recvfrom(512)
                    print("Appairage - requete DNS de", addr)
                    response = dns_response(query, AP_IP)
                    if response:
                        dns_sock.sendto(response, addr)
                except Exception as e:
                    print("Appairage - erreur DNS :", e)
            elif sock is http_sock:
                try:
                    conn, addr = http_sock.accept()
                    saved = handle_http(conn) or saved
                except Exception as e:
                    print("Appairage - erreur accept :", e)

    print("Appairage - redemarrage dans 3 s...")
    time.sleep(3)
    machine.reset()


# Verifie si le bouton BOOTSEL est presse pour lancer le mode appairage
def check_pairing_button():
    try:
        if rp2.bootsel_button() == 1:
            print("Bouton BOOTSEL presse : passage en mode appairage")
            # Ne retourne jamais : redemarre apres enregistrement de la config
            pairing_mode()
    except Exception as e:
        print("Erreur de lecture du bouton BOOTSEL :", e)

# Attente en surveillant le bouton BOOTSEL
def sleep_with_pairing_check(duration_seconds):
    deadline = time.ticks_add(time.ticks_ms(), int(duration_seconds * 1000))
    while time.ticks_diff(deadline, time.ticks_ms()) > 0:
        check_pairing_button()
        time.sleep_ms(200)

# Fonction de gestion des exceptions
def handle_exception(e, error_code):
    print("Erreur :", e)
        
    # Activer le clignotement d'erreur
    error_blink(error_code, 60)

    # Reconnecter seulement la ressource liée à l'erreur pour éviter de masquer
    # un problème serveur temporaire par des cycles de reconnexion inutiles.
    if error_code == 4:
        while not temperature_probe_connect():
            sleep_with_pairing_check(1)
    elif error_code == 8:
        while not tds_probe_connect():
            sleep_with_pairing_check(1)
    elif error_code == 5:
        if wlan is None or not wlan.isconnected():
            while not wifi_connect():
                sleep_with_pairing_check(1)

# -------------------------
# PHASE D'INITIALISATION
# -------------------------

# Variables globales pour la sonde de temperature

ds = None
rom = None

# Variable globale pour la sonde TDS
tds_adc = None

# Variables globales pour la led RGB
pwm_r = None
pwm_g = None
pwm_b = None

# Variable globale pour la connexion WiFi
wlan = None

# Variable globale pour l'URL du serveur
server_url = None

# Indicateur pour identifier un reboot côté serveur
first_loop = True

# Dernier code d'erreur rencontré (None si la dernière boucle s'est bien passée)
last_error_code = None

# Couleur affichée pendant l'attente avant d'avoir reçu une config du serveur
current_led_color = DEFAULT_LED_COLOR

# Fréquence d'envoi par défaut (en secondes)
sendFrequencyInSeconds = 60

# Initialisation de la sonde TDS
tds_adc = ADC(Pin(TDS_PIN))

# Initialisation de la LED de statut
pwm_r = machine.PWM(machine.Pin(LED_PIN_R), freq=FREQ)
pwm_g = machine.PWM(machine.Pin(LED_PIN_G), freq=FREQ)
pwm_b = machine.PWM(machine.Pin(LED_PIN_B), freq=FREQ)

# Clignotement long au démarrage
led_set_color(*DEFAULT_LED_COLOR)
time.sleep(4)
led_turn_off()

# Passage en mode appairage si le bouton BOOTSEL est presse au demarrage
check_pairing_button()

# Récupération de l'ID unique de la machine
uid = get_unique_id()

# Connexion à la sonde de température (non bloquant : on continue même si elle ne répond pas)
if not temperature_probe_connect():
    print("Sonde de température indisponible, on continue sans elle")

# Vérification de la sonde TDS (non bloquant : on continue même si elle ne répond pas)
if not tds_probe_connect():
    print("Sonde TDS indisponible, on continue sans elle")

# Connexion au Wi-Fi
while not wifi_connect():
    time.sleep(1)

# -------------------------
# BOUCLE PRINCIPALE
# -------------------------
while True:
    
    # Led allumée en blanc pendant la lecture de la sonde et l'envoi des données
    led_set_color(255, 255, 255)

    # Lecture de la température
    try:
        # temps nécessaire pour la conversion, c'est comme ça dans la doc :(
        ds.convert_temp()
        time.sleep_ms(750)

        temp = ds.read_temp(rom)
        if temp <= 0:
            raise Exception("Température invalide : {}".format(temp))
        print("Température :", temp, "°C")
        
    except Exception as e:
        # Erreur de lecture de la sonde de température : on continue avec une valeur vide
        print("Erreur de lecture de la sonde de température :", e)
        temp = None


    # Lecture TDS (moyenne sur TDS_SAMPLES mesures)
    try:
        tds_ppm, avg_voltage = read_tds_ppm(temp if temp is not None else 25.0)
        validate_tds(tds_ppm)
        print("TDS : {:.0f} ppm (tension : {:.3f} V)".format(tds_ppm, avg_voltage))

    except Exception as e:
        # Erreur de lecture de la sonde TDS : on continue avec une valeur vide
        print("Erreur de lecture de la sonde TDS :", e)
        tds_ppm = None

    try:
        # Vérifier la connexion WiFi avant l'envoi
        if not wlan.isconnected():
            print("WiFi déconnecté, reconnexion...")
            if not wifi_connect():
                raise Exception("Impossible de rétablir la connexion Wi-Fi")
        
        rssi = wlan.status('rssi')
        print("RSSI :", rssi, " db")
        
        payload = {
            "MachineName": uid,
            "Temperature": str(temp) if temp is not None else "",
            "Tds": str(round(tds_ppm, 2)) if tds_ppm is not None else "",
            "Rssi": rssi,
            "FirstLoop": first_loop
        }

        r = None
        try:
            print("Envoi vers :", server_url)
            r = urequests.post(server_url, json=payload, timeout=15)
            print("Réponse serveur :", r.text)
            responseData = r.json()
        finally:
            if r is not None:
                r.close()

        if not isinstance(responseData, dict):
            raise Exception("Réponse serveur invalide")

        current_led_color = get_response_led_color(responseData)
        print("Couleur serveur :", current_led_color)
        
        try:
            sendFrequencyInSeconds = int(responseData.get("sendFrequencyInSeconds", 60))
        except (TypeError, ValueError):
            sendFrequencyInSeconds = 60

        # S'assurer que la fréquence d'envoi est d'au moins 1 seconde
        if sendFrequencyInSeconds < 1:
            sendFrequencyInSeconds = 1

        print("sendFrequencyInSeconds :", sendFrequencyInSeconds)
        
    except Exception as e:
        # Erreur de communication avec le serveur, code 5
        handle_exception(e, 5)
        continue
        
    first_loop = False

    # Boucle terminée sans erreur : on efface le dernier code d'erreur
    last_error_code = None

    # Led allumée pendant la période d'attente pour indiquer que tout va bien
    led_set_color(*current_led_color)

    # Attente avant le prochain envoi en fonction de la configuration dans la table probe
    # (avec surveillance du bouton BOOTSEL pour le mode appairage)
    sleep_with_pairing_check(sendFrequencyInSeconds)


