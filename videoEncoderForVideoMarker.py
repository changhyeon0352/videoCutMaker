import os
import subprocess
import re
from pathlib import Path

def convert_video(input_path, output_path, x, y, start_time, end_time, width, height):
    # 회전 옵션 확인
    rotation_filter = ""
    if "(cw)" in input_path.name:  # 시계 방향 90도 회전
        rotation_filter = "transpose=1"
    elif "(ccw)" in input_path.name:  # 반시계 방향 90도 회전
        rotation_filter = "transpose=2"
    
    if "[R]" in input_path.name:
        # [R] 옵션이 있는 경우 (인코딩 없이 복사)
        command = [
            "ffmpeg",
            "-i", str(input_path),     # 입력 파일
            "-ss", str(start_time),    # 시작 시간
            "-to", str(end_time),      # 끝 시간
            "-c", "copy",              # 비디오와 오디오 스트림을 재인코딩하지 않고 복사
            str(output_path)           # 출력 파일 경로
        ]
    else:
        # 필터 체인 생성
        filter_chain = []
        
        # crop 필터 추가 (필요한 경우)
        if width > 0 and height > 0:
            filter_chain.append(f"crop={width}:{height}:{x}:{y}")
        
        # 회전 필터 추가 (필요한 경우)
        if rotation_filter:
            filter_chain.append(rotation_filter)
        
        # 필터 체인을 쉼표로 연결
        vf_option = ",".join(filter_chain) if filter_chain else None
        
        # 기본 명령어 구성
        command = [
            "ffmpeg",
            "-i", str(input_path),
            "-ss", str(start_time),   # 시작 시간
            "-to", str(end_time),     # 끝 시간
            "-c:v", "hevc_nvenc",
            "-cq:v", "32"
        ]
        
        # 필터가 있는 경우 추가
        if vf_option:
            command.extend(["-vf", vf_option])
        
        # 출력 파일 경로 추가
        command.append(str(output_path))

    # ffmpeg 실행
    subprocess.run(command)

def extract_video_info(filename):
    # 회전 정보가 있는 경우와 없는 경우 모두 처리하는 패턴
    pattern = r"\[(?:\((cw|ccw)\))?(\(\d+_\d+\))_([^]]+)\]"
    match = re.search(pattern, filename)

    if match:
        rotation = match.group(1)  # 'cw', 'ccw' 또는 None
        resolution = match.group(2)  # '(width_height)'
        segment_info = match.group(3)  # 세그먼트 정보
        
        # 해상도 파싱 (괄호 제거 후 처리)
        resolution = resolution.strip('()')
        width, height = map(int, resolution.split('_'))
        
        # 구간 정보를 추출
        segments = []
        segment_pattern = r"x([0-9A-Z]+)y([0-9A-Z]+)t([0-9A-Z]+)-([0-9A-Z]+)"
        for seg in re.finditer(segment_pattern, segment_info):
            x = base36_to_decimal(seg.group(1))
            y = base36_to_decimal(seg.group(2))
            start_time = float(f"{float((base36_to_decimal(seg.group(3)) * 0.1)):.1f}")
            end_time = float(f"{float((base36_to_decimal(seg.group(4)) * 0.1)):.1f}")
            segments.append((x,y,start_time, end_time))
        
        return width, height, segments
    else:
        raise ValueError("파일명에 [width_height] 형식이 없습니다.")

def create_segments(input_path, segments,width,height):
    output_files = []
    for index, (x,y,start, end) in enumerate(segments):
        # 잘린 파일 이름 설정
        output_file = f"[S{index}]{input_path.stem}.mp4"  # mp4 확장자 추가
        output_path = input_path.parent / output_file
        
        # 구간에 해당하는 비디오 변환 실행
        convert_video(input_path, output_path,x,y, start, end,width,height)
        output_files.append(output_file)
    
    return output_files

def concatenate_videos(output_path, input_files):
    # 합칠 비디오 파일 목록을 텍스트 파일로 생성
    with open("file_list.txt", "w" , encoding='utf-8') as f:
        for file in input_files:
            f.write(f"file '{file}'\n")
    
    # ffmpeg를 사용하여 비디오 파일 합치기
    command = [
        "ffmpeg",
        "-f", "concat",
        "-safe", "0",
        "-i", "file_list.txt",
        "-c", "copy",
        str(output_path)
    ]
    
    # ffmpeg 실행
    subprocess.run(command)
    os.remove("file_list.txt")  # 생성된 텍스트 파일 삭제

def base36_to_decimal(base36_str):
    realTime = int(base36_str,36)
    return realTime

def main():
    # 현재 폴더 내의 동영상 파일 찾기
    video_extensions = [".mp4", ".mov", ".mkv",".ts",".m4v"]  # 필요에 따라 확장자 추가 가능
    current_directory = Path(".")
    print(current_directory)
    
    for file in current_directory.iterdir():
        if file.suffix.lower() in video_extensions:
            print(file)
            width, height, segments = extract_video_info(file.stem)
            print(f"Width: {width}, Height: {height}, Segments: {segments}")

            # 잘린 구간의 파일 저장
            segment_files = create_segments(file, segments,width,height)
            print(f"Created segments: {segment_files}")

            # 최종 합칠 파일 이름 설정
            cleanFileName = re.sub(r'\[.*?\]', '', f"{file.stem}.mp4").strip();
            final_output_file = f"[RR]{cleanFileName}"
            final_output_path = current_directory / final_output_file
            
            # 비디오 합치기
            concatenate_videos(final_output_path, segment_files)
            print(f"Combined video saved as: {final_output_file}")
            for segmentFile in segment_files:
                os.remove(segmentFile)
            segment_files.clear()

if __name__ == "__main__":
    main()
