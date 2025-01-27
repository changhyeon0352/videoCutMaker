using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCifs.Smb;
using System.IO;

namespace VideoCutMarker
{
    class SMBMgr
    {
		public async Task UploadFileToSmb(string localFilePath, string smbUrl, string username, string password)
		{
			try
			{
				// SMB 연결 설정
				var auth = new NtlmPasswordAuthentication(null, username, password);
				var smbFile = new SmbFile(smbUrl, auth);

				// 로컬 파일 읽기
				using var localFileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read);
				using var smbFileStream = smbFile.GetOutputStream();

				// 파일 업로드
				await localFileStream.CopyToAsync(smbFileStream);
				Console.WriteLine("파일 업로드 성공: " + smbUrl);
			}
			catch (Exception ex)
			{
				Console.WriteLine("파일 업로드 실패: " + ex.Message);
			}
		}


		public async Task DownloadFileFromSmb(string smbUrl, string localFilePath, string username, string password)
		{
			try
			{
				// SMB 연결 설정
				var auth = new NtlmPasswordAuthentication(null, username, password);
				var smbFile = new SmbFile(smbUrl, auth);

				// SMB 파일 읽기
				using var smbFileStream = smbFile.GetInputStream();
				using var localFileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write);
				using var wrappedStream = new InputStreamWrapper(smbFileStream);
				// 파일 다운로드
				await wrappedStream.CopyToAsync(localFileStream);
				Console.WriteLine("파일 다운로드 성공: " + localFilePath);
			}
			catch (Exception ex)
			{
				Console.WriteLine("파일 다운로드 실패: " + ex.Message);
			}
		}

	}
}
