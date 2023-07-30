import glob
import os


pattern = 'Assets/StreamingAssets/*.gif'


for filepath in glob.glob(pattern):
	print(filepath)
	os.system('magick ' + filepath + ' -coalesce ' + filepath)
