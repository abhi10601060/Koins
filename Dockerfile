FROM --platform=linux/amd64 jenkins/agent:latest

USER root

ENV ANDROID_SDK_ROOT=/opt/android-sdk
ENV ANDROID_HOME=/opt/android-sdk

ENV PATH=${PATH}:${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin:${ANDROID_SDK_ROOT}/platform-tools:${ANDROID_SDK_ROOT}/build-tools/35.0.0

RUN apt-get update && apt-get install -y \
    wget \
    unzip \
    curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p ${ANDROID_SDK_ROOT}/cmdline-tools

RUN wget -q https://dl.google.com/android/repository/commandlinetools-linux-13114758_latest.zip  -O /tmp/cmdline-tools.zip \
    && unzip -q /tmp/cmdline-tools.zip -d ${ANDROID_SDK_ROOT}/cmdline-tools \
    && mv ${ANDROID_SDK_ROOT}/cmdline-tools/cmdline-tools ${ANDROID_SDK_ROOT}/cmdline-tools/latest \
    && rm /tmp/cmdline-tools.zip

RUN yes | ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --licenses

RUN java -version && \
    git --version && \
    ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager --list_installed 

RUN ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager "platforms;android-35" \
    && ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager "build-tools;35.0.0" \
    && ${ANDROID_SDK_ROOT}/cmdline-tools/latest/bin/sdkmanager "platform-tools" 

RUN wget -q https://services.gradle.org/distributions/gradle-8.11.1-bin.zip -O /tmp/gradle.zip \
    && unzip -q /tmp/gradle.zip -d /opt \
    && ln -s /opt/gradle-8.11.1/bin/gradle /usr/bin/gradle \
    && rm /tmp/gradle.zip

RUN chown -R jenkins:jenkins ${ANDROID_SDK_ROOT}

USER jenkins

WORKDIR /home/jenkins

CMD ["tail", "-f", "/dev/null"]