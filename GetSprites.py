import re
import requests
  
web_folder = 'https://img.pokemondb.net/sprites/black-white/anim/normal/'
local_folder = 'Assets/Sprites/'
index_filepath = 'index.txt'
extension = '.gif'

index_file = open(index_filepath, 'r')
names = re.findall('(?<=href="/sprites/)[^"]+(?=")', index_file.read())
index_file.close()

for name in names:
	filename = name + extension
	print(filename)
	data = requests.get(web_folder + filename)
	if (data.ok):
		f = open(local_folder + filename, 'wb')
		f.write(data.content)
	f.close()
