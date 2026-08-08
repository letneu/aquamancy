from machine import ADC, Pin
import utime

# Entrée analogique
tds_pin = ADC(Pin(28))

# Tension de référence du Pico
VREF = 3.3
ADC_RESOLUTION = 65535  # ADC 16 bits (valeur brute sur 12 bits réels, mise à l'échelle)

def read_voltage():
    raw = tds_pin.read_u16()
    voltage = raw * VREF / ADC_RESOLUTION
    return voltage

def voltage_to_tds(voltage, temperature=25.0):
    # Compensation de température (formule DFRobot)
    compensation_coefficient = 1.0 + 0.02 * (temperature - 25.0)
    compensated_voltage = voltage / compensation_coefficient
    
    # Formule polynomiale DFRobot pour convertir tension -> ppm
    tds_value = (133.42 * compensated_voltage**3 
                 - 255.86 * compensated_voltage**2 
                 + 857.39 * compensated_voltage) * 0.5
    return tds_value

while True:
    # Moyenne sur plusieurs mesures pour stabiliser
    samples = []
    for _ in range(30):
        samples.append(read_voltage())
        utime.sleep_ms(10)
    
    avg_voltage = sum(samples) / len(samples)
    tds_ppm = voltage_to_tds(avg_voltage)
    
    print("Tension: {:.3f} V | TDS: {:.0f} ppm".format(avg_voltage, tds_ppm))
    utime.sleep(1)