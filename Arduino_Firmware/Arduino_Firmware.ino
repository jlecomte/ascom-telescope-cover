/*
 * Arduino_Firmware.ino
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

#include <Servo.h>

constexpr auto DEVICE_GUID = "b45ba2c9-f554-4b4e-a43c-10605ca3b84d";

constexpr auto COMMAND_PING = "COMMAND:PING";
constexpr auto RESULT_PING = "RESULT:PING:OK:";

constexpr auto COMMAND_INFO = "COMMAND:INFO";
constexpr auto RESULT_INFO = "RESULT:DarkSkyGeek's Telescope Cover Firmware v1.0";

constexpr auto COMMAND_COVER_OPEN = "COMMAND:COVER:OPEN";
constexpr auto COMMAND_COVER_CLOSE = "COMMAND:COVER:CLOSE";

constexpr auto COMMAND_COVER_GETSTATE = "COMMAND:COVER:GETSTATE";
constexpr auto RESULT_COVER_STATE_OPEN = "RESULT:COVER:GETSTATE:OPEN";
constexpr auto RESULT_COVER_STATE_CLOSED = "RESULT:COVER:GETSTATE:CLOSED";

constexpr auto COMMAND_CALIBRATOR_ON = "COMMAND:CALIBRATOR:ON";
constexpr auto COMMAND_CALIBRATOR_OFF = "COMMAND:CALIBRATOR:OFF";

constexpr auto COMMAND_CALIBRATOR_GETSTATE = "COMMAND:CALIBRATOR:GETSTATE";
constexpr auto RESULT_CALIBRATOR_STATE_ON = "RESULT:CALIBRATOR:GETSTATE:ON";
constexpr auto RESULT_CALIBRATOR_STATE_OFF = "RESULT:CALIBRATOR:GETSTATE:OFF";

constexpr auto ERROR_INVALID_COMMAND = "ERROR:INVALID_COMMAND";

// Pins controlling the calibrator and the servo. Change these depending on your exact wiring!
const unsigned int CALIBRATOR_SWITCH_PIN = 7;
const unsigned int MOTOR_CONTROL_PIN = 9;

enum CoverState {
  open,
  closed
} coverState;

enum CalibratorState {
  on,
  off
} calibratorState;

Servo servo;

// The `setup` function runs once when you press reset or power the board.
void setup() {
  // Initialize serial port I/O.
  Serial.begin(57600);
  while (!Serial) {
    ;  // Wait for serial port to connect. Required for native USB!
  }
  Serial.flush();

  // Initialize pins...
  pinMode(CALIBRATOR_SWITCH_PIN, OUTPUT);
  pinMode(MOTOR_CONTROL_PIN, OUTPUT);

  // Make sure the RX, TX, and built-in LEDs don't turn on, they are very bright!
  // Even though the board is inside an enclosure, the light can be seen shining
  // through the small opening for the USB connector! Unfortunately, it is not
  // possible to turn off the power LED (green) in code...
  pinMode(PIN_LED_TXL, INPUT);
  pinMode(PIN_LED_RXL, INPUT);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, HIGH);

  // Make sure the calibrator is initially turned off...
  digitalWrite(CALIBRATOR_SWITCH_PIN, LOW);
  calibratorState = off;

  // Initialize servo.
  // Important: We assume that the cover is in the closed position!
  // If it's not, then the servo will brutally close it when the system is powered up!
  // That may damage the mechanical parts, so be careful...
  servo.attach(MOTOR_CONTROL_PIN);
  servo.write(0);
  coverState = closed;
}

// The `loop` function runs over and over again until power down or reset.
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
  int pos = servo.read();

  if (pos < 180) {
    for (; pos <= 180; pos++) {
      servo.write(pos);
      delay(30);
    }
  }

  coverState = open;
}

void closeCover() {
  int pos = servo.read();

  if (pos > 0) {
    for (; pos >= 0; pos--) {
      servo.write(pos);
      delay(30);
    }
  }

  coverState = closed;
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
