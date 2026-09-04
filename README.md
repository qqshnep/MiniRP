# MiniRP
## Step07:Directional Shadow Map

### 1. 阴影贴图
分辨率 1024 x 1024 , 利用率很低
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/01.png)

### 2. Shadow Acne 自阴影干涉
阴影表面正在拿自己写入 Shadow Map 的深度和自己当前的深度做比较，两个值因为精度/采样位置误差出现微小差异，于是有些 Pixel 被错误判断为“被遮挡”。
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/02.png)
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/06.png)

### 3. 增加 Depth Bias
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/04.png)

### 4. 增加 Normal Bias
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/08.png)

### 5. 增加 PCF 领域采样
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/10.png)

### 5. 增加分辨率 1024->2048
![image](https://github.com/qqshnep/MiniRP/blob/step07/readme_img/step07/11.png)
   
