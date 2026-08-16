"""
Pilotage d'un module RGB KY-016 (cathode commune) sur Raspberry Pi Pico 2
MicroPython

Branchement :
  R  -> GPIO 10 (via resistance ~220 ohm)
  G  -> GPIO 11 (via resistance ~220 ohm)
  B  -> GPIO 12 (via resistance ~220 ohm)
  -  -> GND
"""

from machine import Pin, PWM
from time import sleep

# --- Configuration des broches PWM ---
PIN_R = 21
PIN_G = 20
PIN_B = 19

FREQ = 1000  # frequence PWM en Hz

pwm_r = PWM(Pin(PIN_R))
pwm_g = PWM(Pin(PIN_G))
pwm_b = PWM(Pin(PIN_B))

for pwm in (pwm_r, pwm_g, pwm_b):
    pwm.freq(FREQ)


def _duty(v):
    """Convertit une valeur 0-255 en duty 0-65535."""
    return int((v / 255) * 65535)


def set_color(r, g, b):
    """
    Definit la couleur de la LED.
    r, g, b : entiers entre 0 (eteint) et 255 (luminosite max)
    """
    print("Couleur -> R:{} G:{} B:{}".format(r, g, b))
    # duty_u16 attend une valeur entre 0 et 65535
    pwm_r.duty_u16(_duty(r))
    pwm_g.duty_u16(_duty(g))
    pwm_b.duty_u16(_duty(b))


def eteindre():
    print("Extinction de la LED")
    set_color(0, 0, 0)


# --- Exemple d'utilisation ---
if __name__ == "__main__":
    print("Demarrage du script RGB KY-016 sur Pico 2")
    print("Broches -> R: GPIO{}  G: GPIO{}  B: GPIO{}".format(PIN_R, PIN_G, PIN_B))

    noms_couleurs = ["Rouge", "Vert", "Bleu", "Jaune", "Cyan", "Magenta", "Rose", "Blanc"]
    couleurs = [
        (255, 0, 0),     # Rouge
        (0, 255, 0),     # Vert
        (0, 0, 255),     # Bleu
        (255, 255, 0),   # Jaune
        (0, 255, 255),   # Cyan
        (255, 0, 255),   # Magenta
        (255, 105, 180), # Rose
        (255, 255, 255), # Blanc
    ]

    try:
        while True:
            for nom, (r, g, b) in zip(noms_couleurs, couleurs):
                print("Affichage : {}".format(nom))
                set_color(r, g, b)
                sleep(2)
    except KeyboardInterrupt:
        print("Arret demande par l'utilisateur (Ctrl+C)")
        eteindre()
























