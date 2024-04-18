/*
 * Arduino_Firmware.ino
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

#include <Servo.h>
#include <FlashStorage.h>

constexpr auto DEVICE_GUID = "b45ba2c9-f554-4b4e-a43c-10605ca3b84d";

constexpr auto COMMAND_PING = "COMMAND:PING";
constexpr auto RESULT_PING = "RESULT:PING:OK:";

constexpr auto COMMAND_INFO = "COMMAND:INFO";
constexpr auto RESULT_INFO = "RESULT:DarkSkyGeek's Telescope Cover Firmware v1.0";

constexpr auto COMMAND_COVER_OPEN = "COMMAND:COVER:OPEN";
constexpr auto COMMAND_COVER_CLOSE = "COMMAND:COVER:CLOSE";

constexpr auto COMMAND_COVER_CALIBRATE = "COMMAND:COVER:CALIBRATE";
constexpr auto RESULT_COVER_CALIBRATE = "RESULT:COVER:CALIBRATE:";

constexpr auto COMMAND_COVER_GETSTATE = "COMMAND:COVER:GETSTATE";
constexpr auto RESULT_COVER_STATE_OPEN = "RESULT:COVER:GETSTATE:OPEN";
constexpr auto RESULT_COVER_STATE_CLOSED = "RESULT:COVER:GETSTATE:CLOSED";

constexpr auto COMMAND_CALIBRATOR_ON = "COMMAND:CALIBRATOR:ON";
constexpr auto COMMAND_CALIBRATOR_OFF = "COMMAND:CALIBRATOR:OFF";

constexpr auto COMMAND_CALIBRATOR_GETSTATE = "COMMAND:CALIBRATOR:GETSTATE";
constexpr auto RESULT_CALIBRATOR_STATE_ON = "RESULT:CALIBRATOR:GETSTATE:ON";
constexpr auto RESULT_CALIBRATOR_STATE_OFF = "RESULT:CALIBRATOR:GETSTATE:OFF";

constexpr auto ERROR_INVALID_COMMAND = "ERROR:INVALID_COMMAND";

// Pins assignment. Change these depending on your exact wiring!
const unsigned int CALIBRATOR_SWITCH_PIN = 6;
const unsigned int SERVO_SWITCH_PIN = 7;
const unsigned int SERVO_FEEDBACK_PIN = 8;
const unsigned int SERVO_CONTROL_PIN = 9;

// Value used to determine whether the NVM (Non-Volatile Memory) was written,
// or we are just reading garbage...
const unsigned int NVM_MAGIC_NUMBER = 0x12345678;

enum CoverState {
  open,
  closed
} coverState;

enum CalibratorState {
  on,
  off
} calibratorState;

typedef struct {
  unsigned int magicNumber;
  double slope;
  double intercept;
} ServoCalibration;

FlashStorage(nvmStore, ServoCalibration);

Servo servo;
ServoCalibration servoCalibrationData;

void setup() {
  // Initialize serial port I/O.
  Serial.begin(57600);
  while (!Serial) {
    ;  // Wait for serial port to connect. Required for native USB!
  }
  Serial.flush();

  // Read servo calibration data oin Flash storage:
  servoCalibrationData = nvmStore.read();

  // Initialize pins...
  pinMode(CALIBRATOR_SWITCH_PIN, OUTPUT);
  pinMode(SERVO_SWITCH_PIN, OUTPUT);
  pinMode(SERVO_FEEDBACK_PIN, INPUT);
  pinMode(SERVO_CONTROL_PIN, OUTPUT);

  // Make sure the RX, TX, and built-in LEDs don't turn on, they are very bright!
  // Even though the board is inside an enclosure, the light can be seen shining
  // through the small opening for the USB connector! Unfortunately, it is not
  // possible to turn off the power LED (green) in code...
  pinMode(PIN_LED_TXL, INPUT);
  pinMode(PIN_LED_RXL, INPUT);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, HIGH);

  // Make sure the servo is initially de-energized...
  digitalWrite(SERVO_SWITCH_PIN, LOW);

  // Make sure the calibrator is initially turned off...
  digitalWrite(CALIBRATOR_SWITCH_PIN, LOW);
  calibratorState = off;

  // Close the cover, in case it is not completely closed.
  // If the cover has not yet been calibrated, this will be a no-op.
  closeCover();
}

void loop() {
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    if (command == COMMAND_PING) {
      handlePing();
    } else if (command == COMMAND_INFO) {
      sendFirmwareInfo();
    } else if (command == COMMAND_COVER_GETSTATE) {
      sendCurrentCoverState();
    } else if (command == COMMAND_COVER_OPEN) {
      openCover();
    } else if (command == COMMAND_COVER_CLOSE) {
      closeCover();
    } else if (command == COMMAND_COVER_CALIBRATE) {
      calibrateCover();
    } else if (command == COMMAND_CALIBRATOR_GETSTATE) {
      sendCurrentCalibratorState();
    }else if (command == COMMAND_CALIBRATOR_ON) {
      turnCalibratorOn();
    } else if (command == COMMAND_CALIBRATOR_OFF) {
      turnCalibratorOff();
    } else {
      handleInvalidCommand();
    }
  }

  // Blink the built-in LED to let the user know that the device needs to be calibrated once!
  // Note: The device needs to be recalibrated every time the firmware is flashed.
  if (servoCalibrationData.magicNumber != NVM_MAGIC_NUMBER) {
    digitalWrite(LED_BUILTIN, HIGH);
    delay(500);
    digitalWrite(LED_BUILTIN, LOW);
    delay(500);
  }
}

void handlePing() {
  Serial.print(RESULT_PING);
  Serial.println(DEVICE_GUID);
}

void sendFirmwareInfo() {
  Serial.println(RESULT_INFO);
}

void sendCurrentCoverState() {
  switch (coverState) {
    case open:
      Serial.println(RESULT_COVER_STATE_OPEN);
      break;
    case closed:
      Serial.println(RESULT_COVER_STATE_CLOSED);
      break;
  }
}

void openCover() {
  if (servoCalibrationData.magicNumber != NVM_MAGIC_NUMBER) {
    return;
  }

  int pos = powerUpServo();

  if (pos < 190) {
    for (; pos <= 190; pos++) {
      servo.write(pos);
      delay(30);
    }
  }

  coverState = open;

  powerDownServo();
}

void closeCover() {
  if (servoCalibrationData.magicNumber != NVM_MAGIC_NUMBER) {
    return;
  }

  int pos = powerUpServo();

  if (pos > 0) {
    for (; pos >= 0; pos--) {
      servo.write(pos);
      delay(30);
    }
  }

  coverState = closed;

  powerDownServo();
}

void calibrateCover() {
  powerUpServo();

  int step = 10;
  int nDataPoints = 1 + 180 / step;

  double x[nDataPoints] = { 0 };
  double y[nDataPoints] = { 0 };

  for (int i = 0, pos = 0; pos <= 180; i++, pos = i * step) {
    servo.write(pos);
    delay(1000);
    int feedbackValue = analogRead(SERVO_FEEDBACK_PIN);
    x[i] = pos;
    y[i] = feedbackValue;
  }

  linearRegression(x, y, nDataPoints, &servoCalibrationData.slope, &servoCalibrationData.intercept);
  servoCalibrationData.magicNumber = NVM_MAGIC_NUMBER;
  nvmStore.write(servoCalibrationData);

  Serial.print(RESULT_COVER_CALIBRATE);
  Serial.print(servoCalibrationData.slope);
  Serial.print(":");
  Serial.println(servoCalibrationData.intercept);

  closeCover();
}

void sendCurrentCalibratorState() {
  switch (calibratorState) {
    case on:
      Serial.println(RESULT_CALIBRATOR_STATE_ON);
      break;
    case off:
      Serial.println(RESULT_CALIBRATOR_STATE_OFF);
      break;
  }
}

void turnCalibratorOn() {
  digitalWrite(CALIBRATOR_SWITCH_PIN, HIGH);
  calibratorState = on;
}

void turnCalibratorOff() {
  digitalWrite(CALIBRATOR_SWITCH_PIN, LOW);
  calibratorState = off;
}

void handleInvalidCommand() {
  Serial.println(ERROR_INVALID_COMMAND);
}

// Energize and attach servo.
int powerUpServo() {
  digitalWrite(SERVO_SWITCH_PIN, HIGH);

  int pos = 0;

  if (servoCalibrationData.magicNumber == NVM_MAGIC_NUMBER) {
    delay(100);

    int feedbackValue = analogRead(SERVO_FEEDBACK_PIN);
    pos = (int)((feedbackValue - servoCalibrationData.intercept) / servoCalibrationData.slope);
  }

  servo.write(pos);
  servo.attach(SERVO_CONTROL_PIN);

  return pos;
}

// Detach and de-energize servo to eliminate any possible sources of vibrations.
// Magnets will keep the cover in position, whether it is open or closed.
void powerDownServo() {
  servo.detach();
  digitalWrite(SERVO_SWITCH_PIN, LOW);
}

// Function to calculate the mean of an array.
double mean(double arr[], int n) {
    double sum = 0.0;
    for (int i = 0; i < n; i++) {
        sum += arr[i];
    }
    return sum / n;
}

// Function to calculate the slope and intercept of a linear regression line.
void linearRegression(double x[], double y[], int n, double *slope, double *intercept) {
    double x_mean = mean(x, n);
    double y_mean = mean(y, n);
    double numerator = 0.0;
    double denominator = 0.0;
    for (int i = 0; i < n; i++) {
        numerator += (x[i] - x_mean) * (y[i] - y_mean);
        denominator += (x[i] - x_mean) * (x[i] - x_mean);
    }
    *slope = numerator / denominator;
    *intercept = y_mean - (*slope * x_mean);
}
