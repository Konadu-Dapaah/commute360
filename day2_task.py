import re
num=re.compile(r'(\d{4})-(\d{3})-(\d{3})')
mo=num.search("My number is 0240-751-558")
mo.group(1)
mo.groups()