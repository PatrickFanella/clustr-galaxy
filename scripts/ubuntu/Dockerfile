FROM ubuntu:24.04
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates python3 git build-essential pulseaudio libgtk-3-0 libnss3 libasound2t64 libpulse0 libglu1-mesa libgl1 libxrandr2 libxcursor1 libxinerama1 libxi6 libxcomposite1 libxtst6 libxml2 libdrm2 libgbm1 libsecret-1-0 libcurl4t64 libunwind8 && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /home/ubuntu/.cache/unity3d /home/ubuntu/.local/share/unity3d && chown -R ubuntu:ubuntu /home/ubuntu
USER ubuntu
ENV UNITY_EDITOR=/opt/unity/Editor/Unity
WORKDIR /project
CMD ["python3", "scripts/validate.py"]
