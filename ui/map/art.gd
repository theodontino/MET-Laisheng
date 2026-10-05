extends RefCounted
## 0.3 presentation assets only. No access to gameplay RNG or saved economic state.
static var catalog:Dictionary={}
static var textures:Dictionary={}
static var thumbnails:Dictionary={}
static var hit_masks:Dictionary={}
static var atlases:Array=[]
static var icons:Dictionary={}
static var visual_catalog:Dictionary={}
static var visual_bindings:Dictionary={}
static func initialize():
	if not catalog.is_empty():return
	var parsed=JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation030/art_catalog.json"))
	if not parsed is Dictionary:return
	catalog=parsed
	for path in catalog.atlases:
		var texture=_load_texture("res://"+path)
		if texture!=null:atlases.append(texture)
	for id in catalog.assets:
		var a=catalog.assets[id]
		if int(a.atlas_page)>=atlases.size():continue
		var texture=AtlasTexture.new();texture.atlas=atlases[int(a.atlas_page)];var r=a.atlas_rect
		texture.region=Rect2(r[0],r[1],r[2],r[3]);texture.filter_clip=true;textures[id]=texture
	var modern="res://content/presentation040/art_catalog.json"
	if FileAccess.file_exists(modern):
		var data=JSON.parse_string(FileAccess.get_file_as_string(modern))
		if data is Dictionary:
			visual_catalog=data
			visual_bindings=data.get("aliases",{}).duplicate()
			visual_bindings.merge(data.get("bindings",{}),true)
			for id in data.get("assets",{}):
				var entry=data.assets[id];var path=str(entry.get("path",""))
				if not path.begins_with("res://"):path="res://"+path
				var texture=_load_texture(path)
				if texture!=null:textures[id]=texture
static func _load_texture(path:String) -> Texture2D:
	# Exported imports retain compression and mipmaps; raw files support source-only runs.
	if ResourceLoader.exists(path):return load(path)
	if not FileAccess.file_exists(path):return null
	var image=Image.new()
	if image.load_png_from_buffer(FileAccess.get_file_as_bytes(path))!=OK:return null
	image.generate_mipmaps()
	return ImageTexture.create_from_image(image)
static func stage_name(tier:int) -> String:
	return ["先遣设施", "定居设施", "都市设施"][clampi(tier,1,3)-1]
static func stage_description(tier:int) -> String:
	return ["模块单体 · 外露设备与独立院落", "永久建筑 · 沿街附房与共享庭院", "复合街区 · 连续街面与整合设备"][clampi(tier,1,3)-1]
static func sprite(id:String) -> Texture2D:
	initialize();return textures.get(asset_id(id),null)
static func thumbnail(id:String) -> Texture2D:
	initialize();var key=asset_id(id)
	if thumbnails.has(key):return thumbnails[key]
	var source=sprite(id)
	if source==null:return null
	var info=metadata(id);var bounds=info.get("bounds",[])
	if bounds.size()!=4:return source
	var texture=AtlasTexture.new();texture.atlas=source
	var rectangle=Rect2(float(bounds[0]),float(bounds[1]),float(bounds[2])-float(bounds[0]),float(bounds[3])-float(bounds[1]))
	texture.region=rectangle.grow(8).intersection(Rect2(Vector2.ZERO,source.get_size()));texture.filter_clip=true
	thumbnails[key]=texture;return texture
static func asset_id(id:String) -> String:
	return str(visual_bindings.get(id,id))
static func opaque_at(id:String,uv:Vector2) -> bool:
	if uv.x<0 or uv.y<0 or uv.x>=1 or uv.y>=1:return false
	var key=asset_id(id)
	if not hit_masks.has(key):
		var info=metadata(id)
		var path=str(info.get("path","")).get_basename()+".mask"
		if not path.begins_with("res://"):path="res://"+path
		if FileAccess.file_exists(path):
			# Exact authored alpha bitset; no GPU texture readback or ASTC decompression.
			hit_masks[key]=FileAccess.get_file_as_bytes(path)
		else:
			var texture=sprite(id)
			if texture==null:return false
			var source=texture.get_image()
			if source.is_compressed() and source.decompress()!=OK:return false
			var mask=BitMap.new();mask.create_from_image_alpha(source,.3)
			hit_masks[key]=mask
	var mask=hit_masks[key]
	if mask is PackedByteArray:
		var width=mask.decode_u32(0);var height=mask.decode_u32(4)
		var index=int(uv.y*height)*width+int(uv.x*width)
		return (mask[8+(index>>3)] & (1<<(index&7)))!=0
	var dimensions=mask.get_size()
	return mask.get_bit(int(uv.x*dimensions.x),int(uv.y*dimensions.y))
static func anchor(id:String) -> Vector2:
	initialize();var info=visual_catalog.get("assets",{}).get(asset_id(id),{})
	var point=info.get("ground_anchor",[.5,.70]);var a=Vector2(float(point[0]),float(point[1]))
	if a.x>1. or a.y>1.:
		var sz=info.get("size",[512,512]);a/=Vector2(float(sz[0]),float(sz[1]))
	return a
static func metadata(id:String) -> Dictionary:
	initialize();return visual_catalog.get("assets",{}).get(asset_id(id),{})
static func icon(id:String,tint:Color=Color("EDF3EF")) -> Texture2D:
	var key=id+":"+tint.to_html()
	if icons.has(key):return icons[key]
	var path="res://assets/ui040/"+id+".svg"
	if not FileAccess.file_exists(path) and not ResourceLoader.exists(path):path="res://assets/ui030/"+id+".svg"
	if not FileAccess.file_exists(path) and not ResourceLoader.exists(path):path="res://assets/ui040/build.svg"
	if not FileAccess.file_exists(path):
		if ResourceLoader.exists(path):
			var imported=load(path);icons[key]=imported;return imported
		return null
	var svg=FileAccess.get_file_as_string(path).replace("#24473f","#"+tint.to_html(false)).replace("#75D7BE","#"+tint.to_html(false)).replace("#C6A56B","#"+tint.to_html(false));var image=Image.new()
	if image.load_svg_from_string(svg,2.0)!=OK:return null
	var tex=ImageTexture.create_from_image(image);icons[key]=tex;return tex
static func picture(id:String,minimum:Vector2=Vector2(56,56)) -> TextureRect:
	var t=TextureRect.new();t.texture=thumbnail(id);t.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;t.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED;t.custom_minimum_size=minimum;t.mouse_filter=Control.MOUSE_FILTER_IGNORE;return t

static var bonus_bindings:Dictionary={}
static func bindings() -> Dictionary:
	if not bonus_bindings.is_empty():return bonus_bindings
	var data=JSON.parse_string(FileAccess.get_file_as_string("res://content/presentation030/bonus_visual_bindings.json"))
	if data is Dictionary:
		for b in data.get("bindings",[]):bonus_bindings[str(b.bonus_id)]=b
	return bonus_bindings
static func attachments(g,building_id:String) -> Array:
	var result=[];var b=g.s.projects[building_id];var mapping=bindings()
	var aliases={"module_storage":"storage","module_heat":"heat","module_recycling":"recycle","module_public_courtyard":"open_court","module_reuse":"reuse","module_heritage":"reuse","module_urban_farm":"roof_farm","module_drone":"drone","module_workshop":"module_workshop"}
	for iid in g.Rules.ids(g.s.bonus_instances):
		var ins=g.s.bonus_instances[iid]
		if not ins.get("active",false):continue
		if ins.host_type=="building" and ins.host_id!=building_id:continue
		if ins.host_type=="organization" and ins.host_id!=b.operator_org:continue
		# Eligibility is checked at installation; upgrading does not remove deployed artwork.
		var item=mapping.get(str(ins.bonus_id),{});var a=aliases.get(str(item.get("asset_id","")),"")
		if a!="" and not a in result:result.append(a)
	return result.slice(0,2)
static func slot_strip(g,layout:Array,active_type:String="") -> HBoxContainer:
	var row=HBoxContainer.new();row.add_theme_constant_override("separation",5);row.mouse_filter=Control.MOUSE_FILTER_IGNORE
	for type in layout:
		var panel=PanelContainer.new();var box=StyleBoxFlat.new();box.bg_color=Color("36584E") if str(type)==active_type else Color("2A3C43");box.border_color=Color("49605F");box.set_border_width_all(1);box.set_corner_radius_all(4);box.content_margin_left=6;box.content_margin_right=6;panel.add_theme_stylebox_override("panel",box)
		var l=Label.new();l.text={"Production":"产","Logistics":"物","Operations":"营","Flex":"◇"}.get(str(type),str(type));l.add_theme_font_size_override("font_size",14);l.mouse_filter=Control.MOUSE_FILTER_IGNORE;panel.add_child(l);row.add_child(panel)
	row.tooltip_text="生产 / 物流 / 运营。填色为本份初始方案占用的类型；完整名称见详情。"
	return row
