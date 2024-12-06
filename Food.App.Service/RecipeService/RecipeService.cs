﻿using AutoMapper.QueryableExtensions;
using Food.App.Core.Entities;
using Food.App.Core.Enums;
using Food.App.Core.Helpers;
using Food.App.Core.Interfaces;
using Food.App.Core.Interfaces.Services;
using Food.App.Core.MappingProfiles;
using Food.App.Core.ViewModels.Recipe;
using Food.App.Core.ViewModels.Recipe.Create;
using Food.App.Core.ViewModels.Response;
using Microsoft.EntityFrameworkCore;

namespace Food.App.Service.RecipeService;
public class RecipeService : IRecipeService
{
    private readonly IRepository<Recipe> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecipeService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _repository = unitOfWork.GetRepository<Recipe>();

    }




    public async Task<ResponseViewModel<PageList<RecipeViewModel>>> GetAll(RecipeParams recipeParams)
    {
        var query = _repository.GetAll()
                               .ProjectTo<RecipeViewModel>();
        var paginatedResult = await PageList<RecipeViewModel>.CreateAsync(query, recipeParams.PageNumber, recipeParams.PageSize);

        return new SuccessResponseViewModel<PageList<RecipeViewModel>>(SuccessCode.RecipesRetrieved, paginatedResult);
    }

    public ResponseViewModel<RecipeViewModel> GetById(int id)
    {
        var query = _repository.GetAll(u => u.Id == id);
        var recipeViewModel = query.ProjectToForFirstOrDefault<RecipeViewModel>();
        if (recipeViewModel is null)
        {
            return new FailureResponseViewModel<RecipeViewModel>(ErrorCode.RecipeNotFound);
        }
        return new SuccessResponseViewModel<RecipeViewModel>(SuccessCode.RecipesRetrieved, recipeViewModel);

    }
    public async Task<ResponseViewModel<int>> Create(CreateRecipeViewModel model)
    {
        var isRecipeExist = await _repository.AnyAsync(x => x.Name == model.Name);
        if (isRecipeExist)
        {
            return new FailureResponseViewModel<int>(ErrorCode.RecipeAlreadyExist);
        }
        var receipe = new Recipe
        {
            Name = model.Name,
            Description = model.Description,
            ImagePath = model.ImagePath,
            CreatedAt = DateTime.UtcNow,
            CategoryId = model.CategoryId,
            RecipeTags = model.TagIds.Select(x => new RecipeTag
            {
                TagId = x
            }).ToList(),
        };
        await _repository.AddAsync(receipe);

        var result = await _unitOfWork.SaveChangesAsync() > 0;

        return result ? new SuccessResponseViewModel<int>(SuccessCode.RecipeCreated, data: receipe.Id)
                      : new FailureResponseViewModel<int>(ErrorCode.DataBaseError);

    }
    public async Task<ResponseViewModel<int>> Update(UpdateRecipeViewModel model)
    {

        var isReciptExist = await _repository.DoesEntityExistAsync(model.RecipeId);
        if (isReciptExist)
        {
            var receipt = new Recipe
            {
                Id = model.RecipeId,
                Name = model.Name,
                Description = model.Description,
                CategoryId = model.CategoryId
            };

            _repository.SaveInclude(receipt, x => x.Name, x => x.Description, x => x.CategoryId);
            await _unitOfWork.SaveChangesAsync();
            return new SuccessResponseViewModel<int>(SuccessCode.RecipeUpdated, data: model.RecipeId);

        }
        else
        {
            return new FailureResponseViewModel<int>(ErrorCode.DataBaseError);
        }
    }
    public async Task<ResponseViewModel<int>> Delete(int id)
    {
        var isRecipeExist = await _unitOfWork.GetRepository<Recipe>()
                                             .AnyAsync(x => x.Id == id && !x.IsDeleted);
        if (isRecipeExist)
        {
            var recipe = new Recipe
            {
                Id = id,
                IsDeleted = true,
            };

            _unitOfWork.GetRepository<Recipe>()
                                              .SaveInclude(recipe, x => x.IsDeleted);
            var saveResult = await _unitOfWork.SaveChangesAsync() > 0;
            var isRecipeHasTag = await _unitOfWork.GetRepository<RecipeTag>()
                                                  .AnyAsync(x => x.RecipeId == id);
            bool recipeTagsUpdated = false;
            if (isRecipeHasTag)
            {
                recipeTagsUpdated = _unitOfWork.GetRepository<RecipeTag>()
                           .GetAll()
                           .Where(x => x.RecipeId == id)
                           .ExecuteUpdate(s => s.SetProperty(b => b.IsDeleted, true)) > 0;

            }
            if (saveResult && isRecipeHasTag && recipeTagsUpdated || saveResult && !isRecipeHasTag)
            {
                return new SuccessResponseViewModel<int>(SuccessCode.RecipeDeleted);
            }
        }
        if (!isRecipeExist)
        {
            return new FailureResponseViewModel<int>(ErrorCode.RecipeNotFound);

        }
        return new FailureResponseViewModel<int>(ErrorCode.DataBaseError);
    }
}
